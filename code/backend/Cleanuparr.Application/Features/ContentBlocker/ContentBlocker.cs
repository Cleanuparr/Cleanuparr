using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ContentBlocker;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.ContentBlocker;
using Cleanuparr.Persistence.Models.Configuration.General;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using LogContext = Serilog.Context.LogContext;

namespace Cleanuparr.Application.Features.ContentBlocker;

public sealed class ContentBlocker : GenericHandler
{
    private readonly BlocklistProvider _blocklistProvider;

    public ContentBlocker(
        ILogger<ContentBlocker> logger,
        DataContext dataContext,
        IMemoryCache cache,
        IBus messageBus,
        ArrClientFactory arrClientFactory,
        ArrQueueIterator arrArrQueueIterator,
        DownloadServiceFactory downloadServiceFactory,
        BlocklistProvider blocklistProvider,
        EventPublisher eventPublisher
    ) : base(
        logger, dataContext, cache, messageBus,
        arrClientFactory, arrArrQueueIterator, downloadServiceFactory, eventPublisher
    )
    {
        _blocklistProvider = blocklistProvider;
    }

    protected override async Task ExecuteInternalAsync()
    {
        if (ContextProvider.Get<List<DownloadClientConfig>>(nameof(DownloadClientConfig)).Count is 0)
        {
            _logger.LogWarning("No download clients configured");
            return;
        }
        
        var config = ContextProvider.Get<ContentBlockerConfig>();
        
        if (!config.Sonarr.Enabled && !config.Radarr.Enabled && !config.Lidarr.Enabled && !config.Readarr.Enabled && !config.Whisparr.Enabled)
        {
            _logger.LogWarning("No blocklists are enabled");
            return;
        }

        await _blocklistProvider.LoadBlocklistsAsync();

        var sonarrConfig = ContextProvider.Get<ArrConfig>(nameof(InstanceType.Sonarr));
        var radarrConfig = ContextProvider.Get<ArrConfig>(nameof(InstanceType.Radarr));
        var lidarrConfig = ContextProvider.Get<ArrConfig>(nameof(InstanceType.Lidarr));
        var readarrConfig = ContextProvider.Get<ArrConfig>(nameof(InstanceType.Readarr));
        var whisparrConfig = ContextProvider.Get<ArrConfig>(nameof(InstanceType.Whisparr));

        if (config.Sonarr.Enabled)
        {
            await ProcessArrConfigAsync(sonarrConfig, InstanceType.Sonarr);
        }
        
        if (config.Radarr.Enabled)
        {
            await ProcessArrConfigAsync(radarrConfig, InstanceType.Radarr);
        }
        
        if (config.Lidarr.Enabled)
        {
            await ProcessArrConfigAsync(lidarrConfig, InstanceType.Lidarr);
        }
        
        if (config.Readarr.Enabled)
        {
            await ProcessArrConfigAsync(readarrConfig, InstanceType.Readarr);
        }
        
        if (config.Whisparr.Enabled)
        {
            await ProcessArrConfigAsync(whisparrConfig, InstanceType.Whisparr);
        }
    }

    protected override async Task ProcessInstanceAsync(ArrInstance instance, InstanceType instanceType)
    {
        IReadOnlyList<string> ignoredDownloads = ContextProvider.Get<GeneralConfig>().IgnoredDownloads;

        using var _ = LogContext.PushProperty(LogProperties.Category, instanceType.ToString());
        
        IArrClient arrClient = _arrClientFactory.GetClient(instanceType);

        // push to context
        ContextProvider.Set(nameof(ArrInstance) + nameof(ArrInstance.Url), instance.Url);
        ContextProvider.Set(nameof(InstanceType), instanceType);
        
        IReadOnlyList<IDownloadService> downloadServices = await GetInitializedDownloadServicesAsync();
        
        await _arrArrQueueIterator.Iterate(arrClient, instance, async items =>
        {
            var groups = items
                .GroupBy(x => x.DownloadId)
                .ToList();
            
            foreach (var group in groups)
            {
                if (group.Any(x => !arrClient.IsRecordValid(x)))
                {
                    continue;
                }
                
                QueueRecord record = group.First();
                
                _logger.LogTrace("processing | {title} | {id}", record.Title, record.DownloadId);
                
                if (!arrClient.IsRecordValid(record))
                {
                    continue;
                }
                
                if (ignoredDownloads.Contains(record.DownloadId, StringComparer.InvariantCultureIgnoreCase))
                {
                    _logger.LogInformation("skip | {title} | ignored", record.Title);
                    continue;
                }
                
                string downloadRemovalKey = CacheKeys.DownloadMarkedForRemoval(record.DownloadId, instance.Url);
                
                if (_cache.TryGetValue(downloadRemovalKey, out bool _))
                {
                    _logger.LogDebug("skip | already marked for removal | {title}", record.Title);
                    continue;
                }
                
                // push record to context
                ContextProvider.Set(nameof(QueueRecord), record);

                BlockFilesResult result = new();

                if (record.Protocol is "torrent")
                {
                    var torrentClients = downloadServices
                        .Where(x => x.ClientConfig.Type is DownloadClientType.Torrent)
                        .ToList();
                    
                    _logger.LogDebug("searching unwanted files for {title}", record.Title);
                    
                    if (torrentClients.Count > 0)
                    {
                        // Check each download client for the download item
                        foreach (var downloadService in torrentClients)
                        {
                            try
                            {
                                // stalled download check
                                result = await downloadService
                                    .BlockUnwantedFilesAsync(record.DownloadId, ignoredDownloads);
                                
                                if (result.Found)
                                {
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error checking download {dName} with download client {cName}", 
                                    record.Title, downloadService.ClientConfig.Name);
                            }
                        }
                    
                        if (!result.Found)
                        {
                            _logger.LogWarning("Download not found in any torrent client | {title}", record.Title);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No torrent clients enabled");
                    }
                }

                if (!result.ShouldRemove)
                {
                    continue;
                }
                
                var config = ContextProvider.Get<ContentBlockerConfig>();
                
                bool removeFromClient = true;
                
                if (result.IsPrivate && !config.DeletePrivate)
                {
                    removeFromClient = false;
                }
                
                await PublishQueueItemRemoveRequest(
                    downloadRemovalKey,
                    instanceType,
                    instance,
                    record,
                    group.Count() > 1,
                    removeFromClient,
                    result.DeleteReason
                );
            }
        });
    }
}