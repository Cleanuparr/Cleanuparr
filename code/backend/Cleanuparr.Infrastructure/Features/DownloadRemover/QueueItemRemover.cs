using System.Net;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Arr;
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
    private readonly ArrClientFactory _arrClientFactory;
    private readonly EventPublisher _eventPublisher;

    public QueueItemRemover(
        ILogger<QueueItemRemover> logger,
        IBus messageBus,
        IMemoryCache cache,
        ArrClientFactory arrClientFactory,
        EventPublisher eventPublisher
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
            var arrClient = _arrClientFactory.GetClient(request.InstanceType);
            await arrClient.DeleteQueueItemAsync(request.Instance, request.Record, request.RemoveFromClient, request.DeleteReason);

            // Set context for EventPublisher
            ContextProvider.Set("downloadName", request.Record.Title);
            ContextProvider.Set("hash", request.Record.DownloadId);
            ContextProvider.Set(nameof(QueueRecord), request.Record);
            ContextProvider.Set(nameof(ArrInstance) + nameof(ArrInstance.Url), request.Instance.Url);
            ContextProvider.Set(nameof(InstanceType), request.InstanceType);

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