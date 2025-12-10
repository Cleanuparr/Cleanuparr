using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Arr.Interfaces;

public interface IArrQueueIterator
{
    Task Iterate(IArrClient arrClient, ArrInstance arrInstance, Func<IReadOnlyList<QueueRecord>, Task> action);
}