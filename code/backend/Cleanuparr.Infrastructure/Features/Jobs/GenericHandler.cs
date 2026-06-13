using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Jobs;

public abstract class GenericHandler : IHandler
{
    protected readonly ILogger<GenericHandler> _logger;
    protected readonly DataContext _dataContext;
    protected readonly IMemoryCache _cache;
    protected readonly IBus _messageBus;
    protected readonly IArrClientFactory _arrClientFactory;
    protected readonly IArrQueueIterator _arrArrQueueIterator;
    protected readonly IDownloadServiceFactory _downloadServiceFactory;
    protected readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly IEventPublisher _eventPublisher;

    protected GenericHandler(
        ILogger<GenericHandler> logger,
        DataContext dataContext,
        IMemoryCache cache,
        IBus messageBus,
        IArrClientFactory arrClientFactory,
        IArrQueueIterator arrArrQueueIterator,
        IDownloadServiceFactory downloadServiceFactory,
        IEventPublisher eventPublisher,
        IDryRunInterceptor dryRunInterceptor
    )
    {
        _logger = logger;
        _cache = cache;
        _messageBus = messageBus;
        _arrClientFactory = arrClientFactory;
        _arrArrQueueIterator = arrArrQueueIterator;
        _downloadServiceFactory = downloadServiceFactory;
        _eventPublisher = eventPublisher;
        _dataContext = dataContext;
        _dryRunInterceptor = dryRunInterceptor;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await DataContext.Lock.WaitAsync();

        try
        {
            ContextProvider.Set(nameof(GeneralConfig), await _dataContext.GeneralConfigs.AsNoTracking().FirstAsync());
            ContextProvider.Set(nameof(InstanceType.Sonarr), await _dataContext.ArrConfigs.AsNoTracking()
                .Include(x => x.Instances)
                .FirstAsync(x => x.Type == InstanceType.Sonarr));
            ContextProvider.Set(nameof(InstanceType.Radarr), await _dataContext.ArrConfigs.AsNoTracking()
                .Include(x => x.Instances)
                .FirstAsync(x => x.Type == InstanceType.Radarr));
            ContextProvider.Set(nameof(InstanceType.Lidarr), await _dataContext.ArrConfigs.AsNoTracking()
                .Include(x => x.Instances)
                .FirstAsync(x => x.Type == InstanceType.Lidarr));
            ContextProvider.Set(nameof(InstanceType.Readarr), await _dataContext.ArrConfigs.AsNoTracking()
                .Include(x => x.Instances)
                .FirstAsync(x => x.Type == InstanceType.Readarr));
            ContextProvider.Set(nameof(InstanceType.Whisparr), await _dataContext.ArrConfigs.AsNoTracking()
                .Include(x => x.Instances)
                .FirstAsync(x => x.Type == InstanceType.Whisparr));
            ContextProvider.Set(nameof(InstanceType.LazyLibrarian), await _dataContext.ArrConfigs.AsNoTracking()
                .Include(x => x.Instances)
                .FirstAsync(x => x.Type == InstanceType.LazyLibrarian));
            ContextProvider.Set(nameof(QueueCleanerConfig), await _dataContext.QueueCleanerConfigs.AsNoTracking().FirstAsync());
            ContextProvider.Set(nameof(ContentBlockerConfig), await _dataContext.ContentBlockerConfigs.AsNoTracking().FirstAsync());
            ContextProvider.Set(nameof(DownloadCleanerConfig), await _dataContext.DownloadCleanerConfigs.AsNoTracking().FirstAsync());
            ContextProvider.Set(nameof(DownloadClientConfig), await _dataContext.DownloadClients.AsNoTracking()
                .Where(x => x.Enabled)
                .ToListAsync());
        }
        finally
        {
            DataContext.Lock.Release();
        }

        await ExecuteInternalAsync(cancellationToken);
    }

    protected abstract Task ExecuteInternalAsync(CancellationToken cancellationToken = default);
    
    protected abstract Task ProcessInstanceAsync(ArrInstance instance);
    
    protected async Task ProcessArrConfigAsync(ArrConfig config, bool throwOnFailure = false)
    {
        var enabledInstances = config.Instances
            .Where(x => x.Enabled)
            .ToList();
        
        if (enabledInstances.Count is 0)
        {
            _logger.LogDebug($"Skip processing {config.Type}. No enabled instances found");
            return;
        }

        foreach (ArrInstance arrInstance in enabledInstances)
        {
            try
            {
                await ProcessInstanceAsync(arrInstance);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "failed to process {type} instance | {url}", config.Type, arrInstance.Url);

                if (throwOnFailure)
                {
                    throw;
                }
            }
        }
    }

    protected async Task PublishQueueItemRemoveRequest(
        string downloadRemovalKey,
        ArrInstance instance,
        QueueRecord record,
        bool isPack,
        bool removeFromClient,
        DeleteReason deleteReason,
        bool skipSearch = false,
        DownloadClientConfig? downloadClient = null,
        bool changeCategory = false
    )
    {
        if (_cache.TryGetValue(downloadRemovalKey, out bool _))
        {
            _logger.LogDebug("skip removal request | already marked for removal | {title}", record.Title);
            return;
        }

        var instanceType = instance.ArrConfig.Type;

        if (instanceType is InstanceType.Sonarr || (instanceType is InstanceType.Whisparr && instance.Version is 2))
        {
            QueueItemRemoveRequest<SeriesSearchItem> removeRequest = new()
            {
                Instance = instance,
                Record = record,
                SearchItem = (SeriesSearchItem)GetRecordSearchItem(instanceType, instance.Version, record, isPack),
                RemoveFromClient = removeFromClient,
                ChangeCategory = changeCategory,
                DeleteReason = deleteReason,
                JobRunId = ContextProvider.GetJobRunId(),
                SkipSearch = skipSearch,
                DownloadClient = downloadClient,
            };

            await _messageBus.Publish(removeRequest);
        }
        else
        {
            QueueItemRemoveRequest<SearchItem> removeRequest = new()
            {
                Instance = instance,
                Record = record,
                SearchItem = GetRecordSearchItem(instanceType, instance.Version, record, isPack),
                RemoveFromClient = removeFromClient,
                ChangeCategory = changeCategory,
                DeleteReason = deleteReason,
                JobRunId = ContextProvider.GetJobRunId(),
                SkipSearch = skipSearch,
                DownloadClient = downloadClient,
            };

            await _messageBus.Publish(removeRequest);
        }

        // Set context for event
        if (downloadClient is not null)
        {
            ContextProvider.SetDownloadClient(downloadClient);
        }

        _logger.LogInformation("item marked for removal | {title} | {url}", record.Title, instance.Url);
        await _eventPublisher.PublishAsync(EventType.DownloadMarkedForDeletion, "Download marked for deletion", EventSeverity.Important,
            data: new { itemName = record.Title, hash = record.DownloadId });
    }
    
    protected SearchItem GetRecordSearchItem(InstanceType type, float version, QueueRecord record, bool isPack = false)
    {
        return type switch
        {
            InstanceType.Sonarr when !isPack => new SeriesSearchItem
            {
                Id = record.EpisodeId,
                SeriesId = record.SeriesId,
                SearchType = SeriesSearchType.Episode
            },
            InstanceType.Sonarr when isPack => new SeriesSearchItem
            {
                Id = record.SeasonNumber,
                SeriesId = record.SeriesId,
                SearchType = SeriesSearchType.Season
            },
            InstanceType.Radarr => new SearchItem
            {
                Id = record.MovieId
            },
            InstanceType.Lidarr => new SearchItem
            {
                Id = record.AlbumId
            },
            InstanceType.Readarr => new SearchItem
            {
                Id = record.BookId
            },
            InstanceType.Whisparr when version is 2 && !isPack => new SeriesSearchItem
            {
                Id = record.EpisodeId,
                SeriesId = record.SeriesId,
                SearchType = SeriesSearchType.Episode
            },
            InstanceType.Whisparr when version is 2 && isPack => new SeriesSearchItem
            {
                Id = record.SeasonNumber,
                SeriesId = record.SeriesId,
                SearchType = SeriesSearchType.Season
            },
            InstanceType.Whisparr when version is 3 => new SearchItem
            {
                Id = record.MovieId
            },
            InstanceType.LazyLibrarian => new SearchItem
            {
                Id = record.BookId
            },
            _ => throw new NotImplementedException($"instance type {type} is not yet supported")
        };
    }

    /// <summary>
    /// LazyLibrarian has no arr-driven <c>removeFromClient</c> equivalent. When we're about
    /// to publish a removal request for a LazyLibrarian item that should also be deleted
    /// from the download client, do that deletion inline using the already-authenticated
    /// download service and the torrent reference we just collected during rule evaluation.
    /// </summary>
    /// <returns>
    /// True if the removal request should proceed; false if the inline deletion failed and
    /// the caller must skip publishing.
    /// </returns>
    protected async Task<bool> TryDeleteForLazyLibrarianAsync(
        InstanceType instanceType,
        bool removeFromClient,
        IDownloadService? downloadService,
        ITorrentItemWrapper? torrent,
        QueueRecord record
    )
    {
        if (instanceType is not InstanceType.LazyLibrarian)
        {
            return true;
        }

        if (!removeFromClient)
        {
            return true;
        }

        if (downloadService is null || torrent is null)
        {
            _logger.LogWarning(
                "skip lazylibrarian delete | torrent reference unavailable | {title} | {hash}",
                record.Title, record.DownloadId
            );
            return false;
        }

        try
        {
            await _dryRunInterceptor.InterceptAsync(() => downloadService.DeleteDownload(torrent, true));
            _logger.LogInformation(
                "torrent removed from download client {client} | {title}",
                downloadService.ClientConfig.Name, record.Title
            );
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "failed to remove torrent from download client {client} | {hash} | {title}",
                downloadService.ClientConfig.Name, record.DownloadId, record.Title
            );
            return false;
        }
    }

    protected async Task<IReadOnlyList<IDownloadService>> GetInitializedDownloadServicesAsync()
    {
        var downloadClientConfigs = ContextProvider.Get<List<DownloadClientConfig>>(nameof(DownloadClientConfig));
        List<IDownloadService> downloadServices = [];

        foreach (var config in downloadClientConfigs)
        {
            try
            {
                var downloadService = _downloadServiceFactory.GetDownloadService(config);
                await downloadService.LoginAsync();
                downloadServices.Add(downloadService);
                _logger.LogDebug("Created download service for {name}", config.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating download service for {name}", config.Name);
            }
        }
        
        if (downloadServices.Count is 0)
        {
            _logger.LogDebug("No valid download clients found");
        }
        else
        {
            _logger.LogDebug("Initialized {count} download clients", downloadServices.Count);
        }
        
        return downloadServices;
    }
}