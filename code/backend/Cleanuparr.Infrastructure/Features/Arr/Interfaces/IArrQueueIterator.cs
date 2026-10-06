using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Arr.Interfaces;

public interface IArrQueueIterator
{
    /// <summary>
    /// Reads every page of the queue, so records sharing a download id are never split across pages.
    /// </summary>
    Task<List<QueueRecord>> ReadAllAsync(IArrClient arrClient, ArrInstance arrInstance);
}