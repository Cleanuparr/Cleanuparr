using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Data.Models.Arr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cleanuparr.Infrastructure.Features.DownloadRemover;

public sealed class QueueItemRemover : IQueueItemRemover
{
    private readonly DataContext _dataContext;
    private readonly IMemoryCache _cache;
    private readonly ArrClientFactory _arrClientFactory;
    private readonly EventPublisher _eventPublisher;

    public QueueItemRemover(
        DataContext dataContext,
        IMemoryCache cache,
        ArrClientFactory arrClientFactory,
        EventPublisher eventPublisher
    )
    {
        _dataContext = dataContext;
        _cache = cache;
        _arrClientFactory = arrClientFactory;
        _eventPublisher = eventPublisher;
    }

    public async Task RemoveQueueItemAsync<T>(QueueItemRemoveRequest<T> request)
        where T : SearchItem
    {
        try
        {
            var generalConfig = await _dataContext.GeneralConfigs
                .AsNoTracking()
                .FirstAsync();
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

            if (!generalConfig.SearchEnabled)
            {
                return;
            }

            await arrClient.SearchItemsAsync(request.Instance, [request.SearchItem]);

            // prevent tracker spamming
            await Task.Delay(TimeSpan.FromSeconds(generalConfig.SearchDelay));
        }
        finally
        {
            _cache.Remove(CacheKeys.DownloadMarkedForRemoval(request.Record.DownloadId, request.Instance.Url));
        }
    }
}