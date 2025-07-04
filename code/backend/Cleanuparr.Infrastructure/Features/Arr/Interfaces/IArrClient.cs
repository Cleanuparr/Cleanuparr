using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Data.Models.Arr;

namespace Cleanuparr.Infrastructure.Features.Arr.Interfaces;

public interface IArrClient
{
    Task<QueueListResponse> GetQueueItemsAsync(ArrInstance arrInstance, int page);

    Task<bool> ShouldRemoveFromQueue(InstanceType instanceType, QueueRecord record, bool isPrivateDownload, short arrMaxStrikes);

    Task DeleteQueueItemAsync(ArrInstance arrInstance, QueueRecord record, bool removeFromClient, DeleteReason deleteReason);
    
    Task SearchItemsAsync(ArrInstance arrInstance, HashSet<SearchItem>? items);
    
    bool IsRecordValid(QueueRecord record);
    
    /// <summary>
    /// Tests the connection to an Arr instance
    /// </summary>
    /// <param name="arrInstance">The instance to test connection to</param>
    /// <returns>Task that completes when the connection test is done</returns>
    Task TestConnectionAsync(ArrInstance arrInstance);
}