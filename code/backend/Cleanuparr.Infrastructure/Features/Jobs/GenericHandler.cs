using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Features.LazyLibrarian;
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
            ContextProvider.Set(nameof(GeneralConfig), await _dataContext.GeneralConfigs.AsNoTracking().FirstAsync(cancellationToken));
            ContextProvider.Set(nameof(InstanceType.Sonarr), await _dataContext.ArrConfigs.AsNoTracking()
                .Include(x => x.Instances)
                .FirstAsync(x => x.Type == InstanceType.Sonarr, cancellationToken));
            ContextProvider.Set(nameof(InstanceType.Radarr), await _dataContext.ArrConfigs.AsNoTracking()
                .Include(x => x.Instances)
                .FirstAsync(x => x.Type == InstanceType.Radarr, cancellationToken));
            ContextProvider.Set(nameof(InstanceType.Lidarr), await _dataContext.ArrConfigs.AsNoTracking()
                .Include(x => x.Instances)
                .FirstAsync(x => x.Type == InstanceType.Lidarr, cancellationToken));
            ContextProvider.Set(nameof(InstanceType.Readarr), await _dataContext.ArrConfigs.AsNoTracking()
                .Include(x => x.Instances)
                .FirstAsync(x => x.Type == InstanceType.Readarr, cancellationToken));
            ContextProvider.Set(nameof(InstanceType.Whisparr), await _dataContext.ArrConfigs.AsNoTracking()
                .Include(x => x.Instances)
                .FirstAsync(x => x.Type == InstanceType.Whisparr, cancellationToken));
            ContextProvider.Set(nameof(InstanceType.Sportarr), await _dataContext.ArrConfigs.AsNoTracking()
                .Include(x => x.Instances)
                .FirstAsync(x => x.Type == InstanceType.Sportarr, cancellationToken));
            ContextProvider.Set(nameof(InstanceType.LazyLibrarian), await _dataContext.ArrConfigs.AsNoTracking()
                .Include(x => x.Instances)
                .FirstAsync(x => x.Type == InstanceType.LazyLibrarian, cancellationToken));
            ContextProvider.Set(nameof(QueueCleanerConfig), await _dataContext.QueueCleanerConfigs.AsNoTracking().FirstAsync(cancellationToken));
            ContextProvider.Set(nameof(ContentBlockerConfig), await _dataContext.ContentBlockerConfigs.AsNoTracking().FirstAsync(cancellationToken));
            ContextProvider.Set(nameof(DownloadCleanerConfig), await _dataContext.DownloadCleanerConfigs.AsNoTracking().FirstAsync(cancellationToken));
            ContextProvider.Set(nameof(DownloadClientConfig), await _dataContext.DownloadClients.AsNoTracking()
                .Where(x => x.Enabled)
                .ToListAsync(cancellationToken));
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
                _logger.LogError(exception, "failed to process {Type} instance | {Url}", config.Type, arrInstance.Url);

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
            _logger.LogDebug("skip removal request | already marked for removal | {Title}", record.Title);
            return;
        }

        InstanceType instanceType = instance.ArrConfig.Type;

        ArrRemovalTarget target = new()
        {
            Record = record,
            SearchItem = GetRecordSearchItem(instanceType, instance.Version, record, isPack),
            RemoveFromClient = removeFromClient,
            ChangeCategory = changeCategory,
        };

        await PublishRemovalRequest(instance, target, deleteReason, skipSearch, downloadClient);
    }

    protected async Task PublishRemovalRequest(
        ArrInstance instance,
        RemovalTarget target,
        DeleteReason deleteReason,
        bool skipSearch = false,
        DownloadClientConfig? downloadClient = null
    )
    {
        QueueItemRemoveRequest removeRequest = new()
        {
            Instance = instance,
            Target = target,
            DeleteReason = deleteReason,
            JobRunId = ContextProvider.GetJobRunId(),
            SkipSearch = skipSearch,
            DownloadClient = downloadClient,
        };

        string downloadRemovalKey = CacheKeys.DownloadMarkedForRemoval(target.DownloadId, instance.Url);
        _cache.Set(downloadRemovalKey, true);

        try
        {
            await _messageBus.Publish(removeRequest);
        }
        catch
        {
            _cache.Remove(downloadRemovalKey);
            throw;
        }

        // Set context for event
        if (downloadClient is not null)
        {
            ContextProvider.SetDownloadClient(downloadClient);
        }

        _logger.LogInformation("item marked for removal | {Title} | {Url}", target.Title, instance.Url);
        await _eventPublisher.PublishAsync(EventType.DownloadMarkedForDeletion, "Download marked for deletion", EventSeverity.Important,
            configure: e =>
            {
                e.ItemTitle = target.Title;
                e.ItemHash = target.DownloadId;
            });
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
            InstanceType.Sportarr when !isPack => new SeriesSearchItem
            {
                Id = record.EpisodeId,
                SeriesId = record.SeriesId,
                SearchType = SeriesSearchType.Episode
            },
            InstanceType.Sportarr when isPack => new SeriesSearchItem
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
            _ => throw new NotImplementedException($"instance type {type} is not yet supported")
        };
    }

    /// <summary>
    /// The client delete comes first.
    /// LazyLibrarian clears a snatch only once the client no longer holds the download.
    /// </summary>
    protected async Task ProcessLazyLibrarianDecisionsAsync(
        ArrInstance instance,
        IReadOnlyList<LazyLibrarianRemovalDecision> decisions
    )
    {
        foreach (LazyLibrarianRemovalDecision decision in decisions)
        {
            string downloadRemovalKey = CacheKeys.DownloadMarkedForRemoval(decision.Item.DownloadId, instance.Url);

            if (_cache.TryGetValue(downloadRemovalKey, out bool _))
            {
                _logger.LogDebug("skip | already marked for removal | {Title}", decision.Item.Title);
                continue;
            }

            bool wanted = decision.RemoveFromClient && !decision.Item.WasAdoptedByLazyLibrarian;
            bool removedFromClient = await TryRemoveFromClientAsync(decision);

            if (wanted && !removedFromClient)
            {
                continue;
            }

            LazyLibrarianRemovalTarget target = new()
            {
                Item = decision.Item,
                RemovedFromClient = removedFromClient,
            };

            try
            {
                await PublishRemovalRequest(instance, target, decision.DeleteReason, downloadClient: decision.DownloadClient);
            }
            catch (Exception exception)
            {
                // A failed publish must not skip the next decision.
                _logger.LogError(
                    exception,
                    "failed to mark item for removal | removed from client: {Removed} | {Hash} | {Title}",
                    removedFromClient, decision.Item.DownloadId, decision.Item.Title
                );
            }
        }
    }

    private async Task<bool> TryRemoveFromClientAsync(LazyLibrarianRemovalDecision decision)
    {
        if (!decision.RemoveFromClient)
        {
            return false;
        }

        // LazyLibrarian refuses to remove a task it adopted, and the files back another seed.
        if (decision.Item.WasAdoptedByLazyLibrarian)
        {
            _logger.LogInformation(
                "keeping torrent | LazyLibrarian adopted it | {Title} | {Hash}",
                decision.Item.Title, decision.Item.DownloadId
            );

            return false;
        }

        if (decision.DownloadService is null || decision.Torrent is null)
        {
            _logger.LogWarning(
                "skip lazylibrarian delete | torrent reference unavailable | {Title} | {Hash}",
                decision.Item.Title, decision.Item.DownloadId
            );

            return false;
        }

        try
        {
            await _dryRunInterceptor.InterceptAsync(() => decision.DownloadService.DeleteDownload(decision.Torrent, true));
            _logger.LogInformation(
                "torrent removed from download client {Client} | {Title}",
                decision.DownloadService.ClientConfig.Name, decision.Item.Title
            );

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "failed to remove torrent from download client {Client} | {Hash} | {Title}",
                decision.DownloadService.ClientConfig.Name, decision.Item.DownloadId, decision.Item.Title
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
                _logger.LogDebug("Created download service for {Name}", config.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating download service for {Name}", config.Name);
            }
        }
        
        if (downloadServices.Count is 0)
        {
            _logger.LogDebug("No valid download clients found");
        }
        else
        {
            _logger.LogDebug("Initialized {Count} download clients", downloadServices.Count);
        }
        
        return downloadServices;
    }
}