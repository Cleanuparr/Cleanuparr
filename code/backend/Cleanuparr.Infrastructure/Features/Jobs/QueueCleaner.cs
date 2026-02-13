using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using LogContext = Serilog.Context.LogContext;

namespace Cleanuparr.Infrastructure.Features.Jobs;

public sealed class QueueCleaner : GenericHandler
{
    public QueueCleaner(
        ILogger<QueueCleaner> logger,
        DataContext dataContext,
        IMemoryCache cache,
        IBus messageBus,
        IArrClientFactory arrClientFactory,
        IArrQueueIterator arrArrQueueIterator,
        IDownloadServiceFactory downloadServiceFactory,
        IEventPublisher eventPublisher
    ) : base(
        logger, dataContext, cache, messageBus,
        arrClientFactory, arrArrQueueIterator, downloadServiceFactory, eventPublisher
    )
    {
    }
    
    protected override async Task ExecuteInternalAsync()
    {
        List<StallRule> stallRules = await _dataContext.StallRules
            .Where(r => r.Enabled)
            .OrderByDescending(r => r.MaxCompletionPercentage)
            .ThenByDescending(r => r.MinCompletionPercentage)
            .AsNoTracking()
            .ToListAsync();
            
        if (stallRules.Count is 0)
        {
            _logger.LogDebug("No active stall rules found");
        }
            
        ContextProvider.Set(nameof(StallRule), stallRules);
            
        List<SlowRule> slowRules = await _dataContext.SlowRules
            .Where(r => r.Enabled)
            .OrderByDescending(r => r.MaxCompletionPercentage)
            .ThenByDescending(r => r.MinCompletionPercentage)
            .AsNoTracking()
            .ToListAsync();
            
        if (slowRules.Count is 0)
        {
            _logger.LogDebug("No active slow rules found");
        }
            
        ContextProvider.Set(nameof(SlowRule), slowRules);
        
        var sonarrConfig = ContextProvider.Get<ArrConfig>(nameof(InstanceType.Sonarr));
        var radarrConfig = ContextProvider.Get<ArrConfig>(nameof(InstanceType.Radarr));
        var lidarrConfig = ContextProvider.Get<ArrConfig>(nameof(InstanceType.Lidarr));
        var readarrConfig = ContextProvider.Get<ArrConfig>(nameof(InstanceType.Readarr));
        var whisparrConfig = ContextProvider.Get<ArrConfig>(nameof(InstanceType.Whisparr));

        await ProcessArrConfigAsync(sonarrConfig);
        await ProcessArrConfigAsync(radarrConfig);
        await ProcessArrConfigAsync(lidarrConfig);
        await ProcessArrConfigAsync(readarrConfig);
        await ProcessArrConfigAsync(whisparrConfig);
    }

    protected override async Task ProcessInstanceAsync(ArrInstance instance)
    {
        List<string> ignoredDownloads = ContextProvider.Get<GeneralConfig>(nameof(GeneralConfig)).IgnoredDownloads;
        QueueCleanerConfig queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>();
        ignoredDownloads.AddRange(queueCleanerConfig.IgnoredDownloads);
        
        using var _ = LogContext.PushProperty(LogProperties.Category, instance.ArrConfig.Type.ToString());
        using var _2 = LogContext.PushProperty(LogProperties.InstanceName, instance.Name);

        IArrClient arrClient = _arrClientFactory.GetClient(instance.ArrConfig.Type, instance.Version);

        // push to context
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, instance.ExternalUrl ?? instance.Url);
        ContextProvider.Set(nameof(InstanceType), instance.ArrConfig.Type);
        ContextProvider.Set(ContextProvider.Keys.Version, instance.Version);

        IReadOnlyList<IDownloadService> downloadServices = await GetInitializedDownloadServicesAsync();
        bool hasEnabledTorrentClients = ContextProvider
            .Get<List<DownloadClientConfig>>(nameof(DownloadClientConfig))
            .Where(x => x.Type == DownloadClientType.Torrent)
            .Any(x => x.Enabled);

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
                    _logger.LogInformation("skip | download is ignored | {name}", record.Title);
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

                DownloadCheckResult downloadCheckResult = new();
                bool isTorrent = record.Protocol.Contains("torrent", StringComparison.InvariantCultureIgnoreCase);

                if (isTorrent)
                {
                    var torrentClients = downloadServices
                        .Where(x => x.ClientConfig.Type is DownloadClientType.Torrent)
                        .ToList();
                    
                    if (torrentClients.Count > 0)
                    {
                        // Check each download client for the download item
                        foreach (var downloadService in torrentClients)
                        {
                            try
                            {
                                // Get torrent info from download service for rule evaluation
                                downloadCheckResult = await downloadService
                                    .ShouldRemoveFromArrQueueAsync(record.DownloadId, ignoredDownloads);
                                
                                if (downloadCheckResult.Found)
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
                    
                        if (!downloadCheckResult.Found)
                        {
                            _logger.LogWarning("Download not found in any torrent client | {title}", record.Title);
                        }
                    }
                }

                if (downloadCheckResult.ShouldRemove)
                {
                    bool removeFromClient = !downloadCheckResult.IsPrivate || downloadCheckResult.DeleteFromClient;

                    await PublishQueueItemRemoveRequest(
                        downloadRemovalKey,
                        instance.ArrConfig.Type,
                        instance,
                        record,
                        group.Count() > 1,
                        removeFromClient,
                        downloadCheckResult.DeleteReason
                    );

                    continue;
                }

                // Skip failed import check if torrent is not found in client and skipIfNotFoundInClient is enabled
                if (isTorrent && hasEnabledTorrentClients && !downloadCheckResult.Found && queueCleanerConfig.FailedImport.SkipIfNotFoundInClient)
                {
                    _logger.LogInformation("skip | torrent not found in any torrent client | {title}", record.Title);
                    continue;
                }

                // Failed import check
                bool shouldRemoveFromArr = await arrClient
                    .ShouldRemoveFromQueue(instance.ArrConfig.Type, record, downloadCheckResult.IsPrivate, instance.ArrConfig.FailedImportMaxStrikes);

                if (shouldRemoveFromArr)
                {
                    bool removeFromClient = !downloadCheckResult.IsPrivate || queueCleanerConfig.FailedImport.DeletePrivate;
                    
                    await PublishQueueItemRemoveRequest(
                        downloadRemovalKey,
                        instance.ArrConfig.Type,
                        instance,
                        record,
                        group.Count() > 1,
                        removeFromClient,
                        DeleteReason.FailedImport
                    );

                    continue;
                }
                
                _logger.LogDebug("skip | {title}", record.Title);
            }
        });
    }
}