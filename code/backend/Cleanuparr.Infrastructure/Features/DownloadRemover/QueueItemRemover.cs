using System.Net;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Data.Models.Arr;
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

    public QueueItemRemover(
        ILogger<QueueItemRemover> logger,
        IMemoryCache cache,
        IArrClientFactory arrClientFactory,
        IEventPublisher eventPublisher,
        EventsContext eventsContext,
        DataContext dataContext
    )
    {
        _logger = logger;
        _cache = cache;
        _arrClientFactory = arrClientFactory;
        _eventPublisher = eventPublisher;
        _eventsContext = eventsContext;
        _dataContext = dataContext;
    }

    public async Task RemoveQueueItemAsync<T>(QueueItemRemoveRequest<T> request)
        where T : SearchItem
    {
        try
        {
            var arrClient = _arrClientFactory.GetClient(request.InstanceType, request.Instance.Version);
            await arrClient.DeleteQueueItemAsync(request.Instance, request.Record, request.RemoveFromClient, request.DeleteReason);

            // Mark the download item as removed in the database
            await _eventsContext.DownloadItems
                .Where(x => EF.Functions.Like(x.DownloadId, request.Record.DownloadId))
                .ExecuteUpdateAsync(setter =>
                {
                    setter.SetProperty(x => x.IsRemoved, true);
                    setter.SetProperty(x => x.IsMarkedForRemoval, false);
                });
            
            // Set context for EventPublisher
            ContextProvider.SetJobRunId(request.JobRunId);
            ContextProvider.Set(ContextProvider.Keys.ItemName, request.Record.Title);
            ContextProvider.Set(ContextProvider.Keys.Hash, request.Record.DownloadId);
            ContextProvider.Set(nameof(QueueRecord), request.Record);
            ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, request.Instance.ExternalUrl ?? request.Instance.Url);
            ContextProvider.Set(nameof(InstanceType), request.InstanceType);
            ContextProvider.Set(ContextProvider.Keys.Version, request.Instance.Version);

            await _eventPublisher.PublishQueueItemDeleted(request.RemoveFromClient, request.DeleteReason);

            string hash = request.Record.DownloadId.ToLowerInvariant();
            var isRecurring = Striker.RecurringHashes.ContainsKey(hash);
            
            if (isRecurring || request.SkipSearch)
            {
                await _eventPublisher.PublishSearchNotTriggered(request.Record.DownloadId, request.Record.Title);
                
                if (isRecurring)
                {
                    Striker.RecurringHashes.Remove(hash, out _);
                }

                return;
            }

            SeekerConfig seekerConfig = await _dataContext.SeekerConfigs
                .AsNoTracking()
                .FirstAsync();

            if (!seekerConfig.SearchEnabled)
            {
                _logger.LogDebug("Search not triggered | {name}", request.Record.Title);
                return;
            }

            _dataContext.SearchQueue.Add(new SearchQueueItem
            {
                ArrInstanceId = request.Instance.Id,
                ItemId = request.SearchItem.Id,
                SeriesId = (request.SearchItem as SeriesSearchItem)?.SeriesId,
                SearchType = (request.SearchItem as SeriesSearchItem)?.SearchType.ToString(),
                Title = request.Record.Title,
            });
            
            await _dataContext.SaveChangesAsync();
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode is not HttpStatusCode.NotFound)
            {
                throw;
            }

            throw new Exception($"Item might have already been deleted by your {request.InstanceType} instance", exception);
        }
        finally
        {
            _cache.Remove(CacheKeys.DownloadMarkedForRemoval(request.Record.DownloadId, request.Instance.Url));
        }
    }
}