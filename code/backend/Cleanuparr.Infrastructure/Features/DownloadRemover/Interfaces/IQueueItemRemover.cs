using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;

namespace Cleanuparr.Infrastructure.Features.DownloadRemover.Interfaces;

public interface IQueueItemRemover
{
    Task RemoveQueueItemAsync(QueueItemRemoveRequest request);
}
