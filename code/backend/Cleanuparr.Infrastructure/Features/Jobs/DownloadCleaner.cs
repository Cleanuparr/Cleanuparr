using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using LogContext = Serilog.Context.LogContext;

namespace Cleanuparr.Infrastructure.Features.Jobs;

public sealed class DownloadCleaner : GenericHandler
{
    private readonly HashSet<string> _downloadsProcessedByArrs = [];
    private readonly TimeProvider _timeProvider;
    private readonly IHardLinkFileService _hardLinkFileService;

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
        IHardLinkFileService hardLinkFileService
    ) : base(
        logger, dataContext, cache, messageBus,
        arrClientFactory, arrArrQueueIterator, downloadServiceFactory, eventPublisher
    )
    {
        _timeProvider = timeProvider;
        _hardLinkFileService = hardLinkFileService;
    }

    protected override async Task ExecuteInternalAsync(CancellationToken cancellationToken = default)
    {
        var downloadServices = await GetInitializedDownloadServicesAsync();

        if (downloadServices.Count is 0)
        {
            _logger.LogWarning("Processing skipped because no download clients are configured");
            return;
        }

        var config = ContextProvider.Get<DownloadCleanerConfig>();

        List<string> ignoredDownloads = ContextProvider.Get<GeneralConfig>(nameof(GeneralConfig)).IgnoredDownloads;
        ignoredDownloads.AddRange(config.IgnoredDownloads);

        var downloadServiceToDownloadsMap = new Dictionary<IDownloadService, List<ITorrentItemWrapper>>();

        foreach (var downloadService in downloadServices)
        {
            using var dcType = LogContext.PushProperty(LogProperties.DownloadClientType, downloadService.ClientConfig.Type.ToString());
            using var dcName = LogContext.PushProperty(LogProperties.DownloadClientName, downloadService.ClientConfig.Name);

            try
            {
                await downloadService.LoginAsync();
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

        if (downloadServiceToDownloadsMap.Count is 0)
        {
            _logger.LogInformation("No seeding downloads found");
            return;
        }

        int totalDownloads = downloadServiceToDownloadsMap.Values.Sum(x => x.Count);
        _logger.LogTrace("Found {count} seeding downloads across {clientCount} clients", totalDownloads, downloadServiceToDownloadsMap.Count);

        // wait for the downloads to appear in the arr queue
        await Task.Delay(TimeSpan.FromSeconds(10), _timeProvider, cancellationToken);

        await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Sonarr)), true);
        await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Radarr)), true);
        await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Lidarr)), true);
        await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Readarr)), true);
        await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Whisparr)), true);

        foreach (var pair in downloadServiceToDownloadsMap)
        {
            List<ITorrentItemWrapper> filteredDownloads = [];

            foreach (ITorrentItemWrapper download in pair.Value)
            {
                if (download.IsIgnored(ignoredDownloads))
                {
                    _logger.LogDebug("skip | download is ignored | {name}", download.Name);
                    continue;
                }

                if (_downloadsProcessedByArrs.Any(x => x.Equals(download.Hash, StringComparison.InvariantCultureIgnoreCase)))
                {
                    _logger.LogDebug("skip | download is used by an arr | {name}", download.Name);
                    continue;
                }

                filteredDownloads.Add(download);
            }

            downloadServiceToDownloadsMap[pair.Key] = filteredDownloads;
        }

        // Process each client with its own per-client config
        foreach (var (downloadService, clientDownloads) in downloadServiceToDownloadsMap)
        {
            using var dcType = LogContext.PushProperty(LogProperties.DownloadClientType, downloadService.ClientConfig.Type.ToString());
            using var dcName = LogContext.PushProperty(LogProperties.DownloadClientName, downloadService.ClientConfig.Name);

            var seedingRules = await LoadSeedingRulesForClient(downloadService.ClientConfig);
            var unlinkedConfig = await LoadUnlinkedConfigForClient(downloadService.ClientConfig.Id);

            if (unlinkedConfig is { Enabled: true })
            {
                if (unlinkedConfig.Categories.Count > 0)
                {
                    await ChangeUnlinkedCategoriesForClientAsync(downloadService, clientDownloads, unlinkedConfig);
                }
                else
                {
                    _logger.LogWarning("Unlinked config is enabled but no categories are configured for {name}, skipping", downloadService.ClientConfig.Name);
                }
            }

            if (seedingRules.Count > 0)
            {
                await CleanDownloadsForClientAsync(downloadService, clientDownloads, seedingRules);
            }
        }

        foreach (var downloadService in downloadServices)
        {
            downloadService.Dispose();
        }
    }

    protected override async Task ProcessInstanceAsync(ArrInstance instance)
    {
        using var _ = LogContext.PushProperty(LogProperties.Category, instance.ArrConfig.Type.ToString());
        using var _2 = LogContext.PushProperty(LogProperties.InstanceName, instance.Name);

        IArrClient arrClient = _arrClientFactory.GetClient(instance.ArrConfig.Type, instance.Version);

        await _arrArrQueueIterator.Iterate(arrClient, instance, async items =>
        {
            var groups = items
                .Where(x => !string.IsNullOrEmpty(x.DownloadId))
                .GroupBy(x => x.DownloadId)
                .ToList();

            foreach (QueueRecord record in groups.Select(group => group.First()))
            {
                _downloadsProcessedByArrs.Add(record.DownloadId.ToLowerInvariant());
            }
        });
    }

    private async Task ChangeUnlinkedCategoriesForClientAsync(
        IDownloadService downloadService,
        List<ITorrentItemWrapper> clientDownloads,
        UnlinkedConfig unlinkedConfig)
    {
        if (unlinkedConfig.IgnoredRootDirs.Count > 0)
        {
            _hardLinkFileService.PopulateFileCounts(unlinkedConfig.IgnoredRootDirs);
        }

        try
        {
            var downloadsToChangeCategory = downloadService
                .FilterDownloadsToChangeCategoryAsync(clientDownloads, unlinkedConfig);

            if (downloadsToChangeCategory?.Count is null or 0)
            {
                return;
            }

            _logger.LogInformation("Evaluating {count} downloads for hardlinks", downloadsToChangeCategory.Count);

            try
            {
                await downloadService.CreateCategoryAsync(unlinkedConfig.TargetCategory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create category {category}", unlinkedConfig.TargetCategory);
            }

            await downloadService.ChangeCategoryForNoHardLinksAsync(downloadsToChangeCategory, unlinkedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process unlinked downloads for {clientName}", downloadService.ClientConfig.Name);
        }

        _logger.LogInformation("Finished hardlinks evaluation");
    }

    private async Task CleanDownloadsForClientAsync(
        IDownloadService downloadService,
        List<ITorrentItemWrapper> clientDownloads,
        List<ISeedingRule> seedingRules)
    {
        try
        {
            var downloadsToClean = downloadService
                .FilterDownloadsToBeCleanedAsync(clientDownloads, seedingRules);

            if (downloadsToClean?.Count is null or 0)
            {
                return;
            }

            _logger.LogInformation("Evaluating {count} downloads for cleanup", downloadsToClean.Count);

            await downloadService.CleanDownloadsAsync(downloadsToClean, seedingRules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean downloads for {clientName}", downloadService.ClientConfig.Name);
        }

        _logger.LogInformation("Finished cleanup evaluation");
    }

    private async Task<List<ISeedingRule>> LoadSeedingRulesForClient(Persistence.Models.Configuration.DownloadClientConfig clientConfig)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            return clientConfig.TypeName switch
            {
                DownloadClientTypeName.qBittorrent => (await _dataContext.QBitSeedingRules
                    .Where(r => r.DownloadClientConfigId == clientConfig.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                DownloadClientTypeName.Deluge => (await _dataContext.DelugeSeedingRules
                    .Where(r => r.DownloadClientConfigId == clientConfig.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                DownloadClientTypeName.Transmission => (await _dataContext.TransmissionSeedingRules
                    .Where(r => r.DownloadClientConfigId == clientConfig.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                DownloadClientTypeName.uTorrent => (await _dataContext.UTorrentSeedingRules
                    .Where(r => r.DownloadClientConfigId == clientConfig.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                DownloadClientTypeName.rTorrent => (await _dataContext.RTorrentSeedingRules
                    .Where(r => r.DownloadClientConfigId == clientConfig.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                _ => []
            };
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    private async Task<UnlinkedConfig?> LoadUnlinkedConfigForClient(Guid clientId)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            return await _dataContext.UnlinkedConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.DownloadClientConfigId == clientId);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
}
