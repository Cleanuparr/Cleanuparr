using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;

namespace Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;

public interface IQueueItemRemover
{
    Task RemoveQueueItemAsync<T>(QueueItemRemoveRequest<T> request) where T : SearchItem;
}