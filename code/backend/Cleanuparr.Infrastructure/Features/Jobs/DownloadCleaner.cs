using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
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
    private readonly HashSet<string> _excludedHashes = [];
    
    public DownloadCleaner(
        ILogger<DownloadCleaner> logger,
        DataContext dataContext,
        IMemoryCache cache,
        IBus messageBus,
        ArrClientFactory arrClientFactory,
        ArrQueueIterator arrArrQueueIterator,
        DownloadServiceFactory downloadServiceFactory,
        EventPublisher eventPublisher
    ) : base(
        logger, dataContext, cache, messageBus,
        arrClientFactory, arrArrQueueIterator, downloadServiceFactory, eventPublisher
    )
    {
    }
    
    protected override async Task ExecuteInternalAsync()
    {
        var downloadServices = await GetInitializedDownloadServicesAsync();

        if (downloadServices.Count is 0)
        {
            _logger.LogWarning("Processing skipped because no download clients are configured");
            return;
        }

        var config = ContextProvider.Get<DownloadCleanerConfig>();
        
        bool isUnlinkedEnabled = config.UnlinkedEnabled && !string.IsNullOrEmpty(config.UnlinkedTargetCategory) && config.UnlinkedCategories.Count > 0;
        bool isCleaningEnabled = config.Categories.Count > 0;

        if (!isUnlinkedEnabled && !isCleaningEnabled)
        {
            _logger.LogWarning("No features are enabled for {name}", nameof(DownloadCleaner));
            return;
        }
        
        List<string> ignoredDownloads = ContextProvider.Get<GeneralConfig>(nameof(GeneralConfig)).IgnoredDownloads;
        ignoredDownloads.AddRange(ContextProvider.Get<DownloadCleanerConfig>().IgnoredDownloads);
        
        var downloadServiceToDownloadsMap = new Dictionary<IDownloadService, List<ITorrentItem>>();

        foreach (var downloadService in downloadServices)
        {
            try
            {
                await downloadService.LoginAsync();
                var clientDownloads = await downloadService.GetSeedingDownloads();
                if (clientDownloads?.Count > 0)
                {
                    downloadServiceToDownloadsMap[downloadService] = clientDownloads;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get seeding downloads from download client {clientName}", downloadService.ClientConfig.Name);
            }
        }

        if (downloadServiceToDownloadsMap.Count == 0)
        {
            _logger.LogDebug("no seeding downloads found");
            return;
        }

        var totalDownloads = downloadServiceToDownloadsMap.Values.Sum(x => x.Count);
        _logger.LogTrace("found {count} seeding downloads across {clientCount} clients", totalDownloads, downloadServiceToDownloadsMap.Count);

        List<Tuple<IDownloadService, List<ITorrentItem>>> downloadServiceWithDownloads = [];

        if (isUnlinkedEnabled)
        {
            // Create category for all clients
            foreach (var downloadService in downloadServices)
            {
                try
                {
                    await downloadService.CreateCategoryAsync(config.UnlinkedTargetCategory);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create category for download client {clientName}", downloadService.ClientConfig.Name);
                }
            }
            
            foreach (var (downloadService, clientDownloads) in downloadServiceToDownloadsMap)
            {
                try
                {
                    var downloadsToChangeCategory = downloadService.FilterDownloadsToChangeCategoryAsync(clientDownloads, config.UnlinkedCategories);
                    if (downloadsToChangeCategory?.Count > 0)
                    {
                        downloadServiceWithDownloads.Add(Tuple.Create(downloadService, downloadsToChangeCategory));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to filter downloads for category change for download client {clientName}", downloadService.ClientConfig.Name);
                }
            }
        }

        // wait for the downloads to appear in the arr queue
        await Task.Delay(10 * 1000);

        await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Sonarr)), InstanceType.Sonarr, true);
        await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Radarr)), InstanceType.Radarr, true);
        await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Lidarr)), InstanceType.Lidarr, true);
        await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Readarr)), InstanceType.Readarr, true);
        await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Whisparr)), InstanceType.Whisparr, true);
        
        if (isUnlinkedEnabled && downloadServiceWithDownloads.Sum(x => x.Item2.Count) > 0)
        {
            _logger.LogInformation("Evaluating {count} downloads for hardlinks", downloadServiceWithDownloads.Sum(x => x.Item2.Count));
            
            // Process each client with its own filtered downloads
            foreach (var (downloadService, downloadsToChangeCategory) in downloadServiceWithDownloads)
            {
                try
                {
                    await downloadService.ChangeCategoryForNoHardLinksAsync(downloadsToChangeCategory, _excludedHashes, ignoredDownloads);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to change category for download client {clientName}", downloadService.ClientConfig.Name);
                }
            }
            
            _logger.LogInformation("Finished hardlinks evaluation");
        }
        
        if (config.Categories.Count is 0)
        {
            return;
        }
        
        downloadServiceWithDownloads = [];
        foreach (var (downloadService, clientDownloads) in downloadServiceToDownloadsMap)
        {
            try
            {
                var downloadsToClean = downloadService.FilterDownloadsToBeCleanedAsync(clientDownloads, config.Categories);
                if (downloadsToClean?.Count > 0)
                {
                    downloadServiceWithDownloads.Add(Tuple.Create(downloadService, downloadsToClean));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to filter downloads for cleaning for download client {clientName}", downloadService.ClientConfig.Name);
            }
        }
        
        _logger.LogInformation("Evaluating {count} downloads for cleanup", downloadServiceWithDownloads.Sum(x => x.Item2.Count));
        
        // Process cleaning for each client
        foreach (var (downloadService, downloadsToClean) in downloadServiceWithDownloads)
        {
            try
            {
                await downloadService.CleanDownloadsAsync(downloadsToClean, config.Categories, _excludedHashes, ignoredDownloads);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean downloads for download client {clientName}", downloadService.ClientConfig.Name);
            }
        }
        
        _logger.LogInformation("Finished cleanup evaluation");

        foreach (var downloadService in downloadServices)
        {
            downloadService.Dispose();
        }
    }

    protected override async Task ProcessInstanceAsync(ArrInstance instance, InstanceType instanceType)
    {
        using var _ = LogContext.PushProperty(LogProperties.Category, instanceType.ToString());
        
        IArrClient arrClient = _arrClientFactory.GetClient(instanceType);
        
        await _arrArrQueueIterator.Iterate(arrClient, instance, async items =>
        {
            var groups = items
                .Where(x => !string.IsNullOrEmpty(x.DownloadId))
                .GroupBy(x => x.DownloadId)
                .ToList();

            foreach (QueueRecord record in groups.Select(group => group.First()))
            {
                _excludedHashes.Add(record.DownloadId.ToLowerInvariant());
            }
        });
    }
}