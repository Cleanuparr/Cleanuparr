using System.Net;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Entities.LazyLibrarian;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.LazyLibrarian;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadRemover;

public sealed class QueueItemRemover : IQueueItemRemover
{
    private readonly ILogger<QueueItemRemover> _logger;
    private readonly IMemoryCache _cache;
    private readonly IArrClientFactory _arrClientFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly EventsContext _eventsContext;
    private readonly DataContext _dataContext;
    private readonly ILazyLibrarianService _lazyLibrarianService;

    public QueueItemRemover(
        ILogger<QueueItemRemover> logger,
        IMemoryCache cache,
        IArrClientFactory arrClientFactory,
        IEventPublisher eventPublisher,
        EventsContext eventsContext,
        DataContext dataContext,
        ILazyLibrarianService lazyLibrarianService
    )
    {
        _logger = logger;
        _cache = cache;
        _arrClientFactory = arrClientFactory;
        _eventPublisher = eventPublisher;
        _eventsContext = eventsContext;
        _dataContext = dataContext;
        _lazyLibrarianService = lazyLibrarianService;
    }

    public async Task RemoveQueueItemAsync(QueueItemRemoveRequest request)
    {
        try
        {
            switch (request.Target)
            {
                case ArrRemovalTarget target:
                    await RemoveViaArrAsync(request, target);
                    break;

                case LazyLibrarianRemovalTarget target:
                    await RemoveViaLazyLibrarianAsync(request, target);
                    break;

                default:
                    throw new NotSupportedException($"removal target {request.Target.GetType().Name} is not supported");
            }
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode is not HttpStatusCode.NotFound)
            {
                throw;
            }

            throw new Exception($"Item might have already been deleted by your {request.Instance.ArrConfig.Type} instance", exception);
        }
        finally
        {
            _cache.Remove(CacheKeys.DownloadMarkedForRemoval(request.Target.DownloadId, request.Instance.Url));
        }
    }

    private async Task RemoveViaArrAsync(QueueItemRemoveRequest request, ArrRemovalTarget target)
    {
        InstanceType instanceType = request.Instance.ArrConfig.Type;
        IArrClient arrClient = _arrClientFactory.GetClient(instanceType, request.Instance.Version);
        await arrClient.DeleteQueueItemAsync(request.Instance, target.Record, target.RemoveFromClient, target.ChangeCategory, request.DeleteReason);

        await MarkDownloadRemovedAsync(target.DownloadId);

        ContextProvider.SetJobRunId(request.JobRunId);
        ContextProvider.Set(ContextProvider.Keys.ItemName, target.Title);
        ContextProvider.Set(ContextProvider.Keys.Hash, target.DownloadId);
        ContextProvider.Set(nameof(QueueRecord), target.Record);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, request.Instance.ExternalOrInternalUrl);
        ContextProvider.Set(nameof(InstanceType), instanceType);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceId, request.Instance.Id);
        ContextProvider.Set(ContextProvider.Keys.Version, request.Instance.Version);

        if (request.DownloadClient is not null)
        {
            ContextProvider.SetDownloadClient(request.DownloadClient);
        }

        await _eventPublisher.PublishQueueItemDeleted(target.RemoveFromClient, request.DeleteReason);

        if (!await ShouldQueueSearchAsync(request, target))
        {
            return;
        }

        _eventsContext.SearchQueue.Add(new SearchQueueItem
        {
            ArrInstanceId = request.Instance.Id,
            ItemId = target.SearchItem.Id,
            SeriesId = (target.SearchItem as SeriesSearchItem)?.SeriesId,
            SearchType = (target.SearchItem as SeriesSearchItem)?.SearchType.ToString(),
            Title = target.Title,
        });

        await _eventsContext.SaveChangesAsync();
    }

    /// <summary>
    /// queueBook leaves the wanted row snatched, and a snatched row blocks every search.
    /// getDownloadProgress is what clears it.
    /// </summary>
    private async Task RemoveViaLazyLibrarianAsync(QueueItemRemoveRequest request, LazyLibrarianRemovalTarget target)
    {
        LazyLibrarianQueueItem item = target.Item;

        await MarkDownloadRemovedAsync(target.DownloadId);

        ContextProvider.SetJobRunId(request.JobRunId);
        ContextProvider.Set(ContextProvider.Keys.ItemName, target.Title);
        ContextProvider.Set(ContextProvider.Keys.Hash, target.DownloadId);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, request.Instance.ExternalOrInternalUrl);
        ContextProvider.Set(nameof(InstanceType), InstanceType.LazyLibrarian);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceId, request.Instance.Id);
        ContextProvider.Set(ContextProvider.Keys.Version, request.Instance.Version);

        if (request.DownloadClient is not null)
        {
            ContextProvider.SetDownloadClient(request.DownloadClient);
        }

        bool snatchCleared = await TryClearSnatchAsync(request, target);

        await _lazyLibrarianService.ResetItemAsync(request.Instance, item);

        _logger.LogInformation(
            "queue item reset in LazyLibrarian with reason {reason} | {url} | {title}",
            request.DeleteReason.ToString(), request.Instance.Url, target.Title
        );

        await _eventPublisher.PublishQueueItemDeleted(target.RemovedFromClient, request.DeleteReason);

        if (!snatchCleared)
        {
            await _eventPublisher.PublishSearchNotTriggered(target.DownloadId, target.Title);
            return;
        }

        if (!await ShouldQueueSearchAsync(request, target))
        {
            return;
        }

        await _lazyLibrarianService.TriggerSearchAsync(request.Instance, item);

        _logger.LogInformation("book search triggered | {url} | book id: {id}", request.Instance.Url, item.BookId);
    }

    /// <summary>
    /// LazyLibrarian marks the wanted row aborted when it polls the client and finds nothing.
    /// Progress -1 means it did. Anything else leaves the row snatched, which blocks the search.
    /// </summary>
    private async Task<bool> TryClearSnatchAsync(QueueItemRemoveRequest request, LazyLibrarianRemovalTarget target)
    {
        if (!target.RemovedFromClient)
        {
            _logger.LogInformation(
                "search not triggered | the torrent is still in the download client | {title}",
                target.Title
            );

            return false;
        }

        LazyLibrarianDownloadProgress? progress =
            await _lazyLibrarianService.GetDownloadProgressAsync(request.Instance, target.Item);

        if (progress is null)
        {
            _logger.LogWarning("search not triggered | no download progress reported | {title}", target.Title);
            return false;
        }

        if (progress.Progress is -1)
        {
            return true;
        }

        _logger.LogWarning(
            "search not triggered | LazyLibrarian still reports the snatch | {title} | progress: {progress}",
            target.Title, progress.Progress
        );

        return false;
    }

    private async Task MarkDownloadRemovedAsync(string downloadId)
    {
        string normalized = downloadId.ToLower();

        await _eventsContext.DownloadItems
            .Where(x => x.DownloadId.ToLower() == normalized)
            .ExecuteUpdateAsync(setter =>
            {
                setter.SetProperty(x => x.IsRemoved, true);
                setter.SetProperty(x => x.IsMarkedForRemoval, false);
            });
    }

    private async Task<bool> ShouldQueueSearchAsync(QueueItemRemoveRequest request, RemovalTarget target)
    {
        string hash = target.DownloadId.ToLowerInvariant();
        bool isRecurring = Striker.RecurringHashes.ContainsKey(hash);

        if (isRecurring || request.SkipSearch)
        {
            await _eventPublisher.PublishSearchNotTriggered(target.DownloadId, target.Title);

            if (isRecurring)
            {
                Striker.RecurringHashes.Remove(hash, out _);
            }

            return false;
        }

        SeekerConfig seekerConfig = await _dataContext.SeekerConfigs
            .AsNoTracking()
            .FirstAsync();

        if (!seekerConfig.SearchEnabled)
        {
            _logger.LogDebug("Search not triggered | {name}", target.Title);
            return false;
        }

        return true;
    }
}
