using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadCleaner.Services;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using LogContext = Serilog.Context.LogContext;

namespace Cleanuparr.Infrastructure.Features.Jobs;

public sealed class DownloadCleaner : GenericHandler
{
    private readonly HashSet<string> _downloadsProcessedByArrs = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _timeProvider;
    private readonly ISeedingRulesCleanupService _seedingRulesService;
    private readonly IUnlinkedDownloadsService _unlinkedService;
    private readonly IOrphanedFilesCleanupService _orphanedFilesService;

    public DownloadCleaner(
        ILogger<DownloadCleaner> logger,
        DataContext dataContext,
        IMemoryCache cache,
        IBus messageBus,
        IArrClientFactory arrClientFactory,
        IArrQueueIterator arrArrQueueIterator,
        IDownloadServiceFactory downloadServiceFactory,
        IEventPublisher eventPublisher,
        TimeProvider timeProvider,
        ISeedingRulesCleanupService seedingRulesService,
        IUnlinkedDownloadsService unlinkedService,
        IOrphanedFilesCleanupService orphanedFilesService
    ) : base(
        logger, dataContext, cache, messageBus,
        arrClientFactory, arrArrQueueIterator, downloadServiceFactory, eventPublisher
    )
    {
        _timeProvider = timeProvider;
        _seedingRulesService = seedingRulesService;
        _unlinkedService = unlinkedService;
        _orphanedFilesService = orphanedFilesService;
    }

    protected override async Task ExecuteInternalAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IDownloadService> downloadServices = await GetInitializedDownloadServicesAsync();

        if (downloadServices.Count is 0)
        {
            _logger.LogWarning("Processing skipped because no download clients are configured");
            return;
        }

        try
        {
            await RunCleanupAsync(downloadServices, cancellationToken);
        }
        finally
        {
            foreach (IDownloadService downloadService in downloadServices)
            {
                try
                {
                    downloadService.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispose download service {name}", downloadService.ClientConfig.Name);
                }
            }
        }
    }

    private async Task RunCleanupAsync(IReadOnlyList<IDownloadService> downloadServices, CancellationToken cancellationToken)
    {
        DownloadCleanerConfig config = ContextProvider.Get<DownloadCleanerConfig>();

        List<string> ignoredDownloads = ContextProvider.Get<GeneralConfig>(nameof(GeneralConfig)).IgnoredDownloads;
        ignoredDownloads.AddRange(config.IgnoredDownloads);

        Dictionary<IDownloadService, List<ITorrentItemWrapper>> downloadServiceToDownloadsMap = new();
        List<IDownloadService> loggedInServices = new();

        foreach (IDownloadService downloadService in downloadServices)
        {
            using IDisposable _ = LogContext.PushProperty(LogProperties.DownloadClientType, downloadService.ClientConfig.Type.ToString());
            using IDisposable _2 = LogContext.PushProperty(LogProperties.DownloadClientName, downloadService.ClientConfig.Name);

            try
            {
                await downloadService.LoginAsync();
                loggedInServices.Add(downloadService);
                List<ITorrentItemWrapper> clientDownloads = await downloadService.GetSeedingDownloads();

                if (clientDownloads.Count > 0)
                {
                    downloadServiceToDownloadsMap[downloadService] = clientDownloads;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get seeding downloads from download client {clientName}", downloadService.ClientConfig.Name);
            }
        }

        int totalDownloads = downloadServiceToDownloadsMap.Values.Sum(x => x.Count);
        _logger.LogTrace("Found {count} seeding downloads across {clientCount} clients", totalDownloads, downloadServiceToDownloadsMap.Count);

        if (downloadServiceToDownloadsMap.Count > 0)
        {
            // wait for the downloads to appear in the arr queue
            await Task.Delay(TimeSpan.FromSeconds(10), _timeProvider, cancellationToken);

            await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Sonarr)), true);
            await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Radarr)), true);
            await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Lidarr)), true);
            await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Readarr)), true);
            await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Whisparr)), true);

            foreach (KeyValuePair<IDownloadService, List<ITorrentItemWrapper>> pair in downloadServiceToDownloadsMap)
            {
                List<ITorrentItemWrapper> filteredDownloads = [];

                foreach (ITorrentItemWrapper download in pair.Value)
                {
                    if (download.IsIgnored(ignoredDownloads))
                    {
                        _logger.LogDebug("skip | download is ignored | {name}", download.Name);
                        continue;
                    }

                    if (_downloadsProcessedByArrs.Contains(download.Hash))
                    {
                        _logger.LogDebug("skip | download is used by an arr | {name}", download.Name);
                        continue;
                    }

                    filteredDownloads.Add(download);
                }

                downloadServiceToDownloadsMap[pair.Key] = filteredDownloads;
            }

            foreach ((IDownloadService downloadService, List<ITorrentItemWrapper> clientDownloads) in downloadServiceToDownloadsMap)
            {
                using IDisposable _ = LogContext.PushProperty(LogProperties.DownloadClientType, downloadService.ClientConfig.Type.ToString());
                using IDisposable _2 = LogContext.PushProperty(LogProperties.DownloadClientName, downloadService.ClientConfig.Name);

                await _unlinkedService.ProcessAsync(downloadService, clientDownloads);
                await _seedingRulesService.CleanAsync(downloadService, clientDownloads);
            }
        }
        else
        {
            _logger.LogInformation("No seeding downloads found, skipping seeding-rule and unlinked-category processing");
        }

        try
        {
            await _orphanedFilesService.ProcessAsync(loggedInServices, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process orphaned files");
        }
    }

    protected override async Task ProcessInstanceAsync(ArrInstance instance)
    {
        using IDisposable _ = LogContext.PushProperty(LogProperties.Category, instance.ArrConfig.Type.ToString());
        using IDisposable _2 = LogContext.PushProperty(LogProperties.InstanceName, instance.Name);

        IArrClient arrClient = _arrClientFactory.GetClient(instance.ArrConfig.Type, instance.Version);

        await _arrArrQueueIterator.Iterate(arrClient, instance, items =>
        {
            List<IGrouping<string, QueueRecord>> groups = items
                .Where(x => !string.IsNullOrEmpty(x.DownloadId))
                .GroupBy(x => x.DownloadId)
                .ToList();

            foreach (QueueRecord record in groups.Select(group => group.First()))
            {
                _downloadsProcessedByArrs.Add(record.DownloadId);
            }

            return Task.CompletedTask;
        });
    }
}
