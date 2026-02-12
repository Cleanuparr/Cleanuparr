using System.Net;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadHunter.Models;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Data.Models.Arr;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadRemover;

public sealed class QueueItemRemover : IQueueItemRemover
{
    private readonly ILogger<QueueItemRemover> _logger;
    private readonly IBus _messageBus;
    private readonly IMemoryCache _cache;
    private readonly IArrClientFactory _arrClientFactory;
    private readonly IEventPublisher _eventPublisher;

    public QueueItemRemover(
        ILogger<QueueItemRemover> logger,
        IBus messageBus,
        IMemoryCache cache,
        IArrClientFactory arrClientFactory,
        IEventPublisher eventPublisher
    )
    {
        _logger = logger;
        _messageBus = messageBus;
        _cache = cache;
        _arrClientFactory = arrClientFactory;
        _eventPublisher = eventPublisher;
    }

    public async Task RemoveQueueItemAsync<T>(QueueItemRemoveRequest<T> request)
        where T : SearchItem
    {
        try
        {
            var arrClient = _arrClientFactory.GetClient(request.InstanceType, request.Instance.Version);
            await arrClient.DeleteQueueItemAsync(request.Instance, request.Record, request.RemoveFromClient, request.DeleteReason);

            // Set context for EventPublisher
            ContextProvider.Set(ContextProvider.Keys.DownloadName, request.Record.Title);
            ContextProvider.Set(ContextProvider.Keys.Hash, request.Record.DownloadId);
            ContextProvider.Set(nameof(QueueRecord), request.Record);
            ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, request.Instance.ExternalUrl ?? request.Instance.Url);
            ContextProvider.Set(nameof(InstanceType), request.InstanceType);
            ContextProvider.Set(ContextProvider.Keys.Version, request.Instance.Version);

            // Use the new centralized EventPublisher method
            await _eventPublisher.PublishQueueItemDeleted(request.RemoveFromClient, request.DeleteReason);

            // If recurring, do not search for replacement
            string hash = request.Record.DownloadId.ToLowerInvariant();
            if (Striker.RecurringHashes.ContainsKey(hash))
            {
                await _eventPublisher.PublishSearchNotTriggered(request.Record.DownloadId, request.Record.Title);
                Striker.RecurringHashes.Remove(hash, out _);
                return;
            }

            await _messageBus.Publish(new DownloadHuntRequest<T>
            {
                InstanceType = request.InstanceType,
                Instance = request.Instance,
                SearchItem = request.SearchItem,
                Record = request.Record
            });
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