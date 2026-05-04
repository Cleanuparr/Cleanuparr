using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Arr.Interfaces;

public interface IArrClient
{
    Task<QueueListResponse> GetQueueItemsAsync(ArrInstance arrInstance, int page);

    Task<bool> ShouldRemoveFromQueue(InstanceType instanceType, QueueRecord record, bool isPrivateDownload, short arrMaxStrikes);

    Task DeleteQueueItemAsync(ArrInstance arrInstance, QueueRecord record, bool removeFromClient, bool changeCategory, DeleteReason deleteReason);

    /// <summary>
    /// Triggers a search for the specified items and returns the arr command IDs
    /// </summary>
    Task<List<long>> SearchItemsAsync(ArrInstance arrInstance, HashSet<SearchItem>? items);

    /// <summary>
    /// Triggers a search for a single item and returns the arr command ID
    /// </summary>
    Task<long> SearchItemAsync(ArrInstance arrInstance, SearchItem item);

    /// <summary>
    /// Gets the status of an arr command by its ID
    /// </summary>
    Task<ArrCommandStatus> GetCommandStatusAsync(ArrInstance arrInstance, long commandId);

    bool IsRecordValid(QueueRecord record);

    /// <summary>
    /// Checks whether the record has an id (movie id, tv show id etc.)
    /// </summary>
    /// <param name="record">The record to check</param>
    /// <returns>True if the record has an id, false otherwise</returns>
    bool HasContentId(QueueRecord record);

    /// <summary>
    /// Tests the connection to an Arr instance
    /// </summary>
    /// <param name="arrInstance">The instance to test connection to</param>
    /// <returns>Task that completes when the connection test is done</returns>
    Task HealthCheckAsync(ArrInstance arrInstance);

    /// <summary>
    /// Returns the number of items actively downloading (SizeLeft > 0) across all queue pages.
    /// Items that are completed, import-blocked, or otherwise finished are not counted.
    /// </summary>
    Task<int> GetActiveDownloadCountAsync(ArrInstance arrInstance);

    Task<List<Tag>> GetAllTagsAsync(ArrInstance arrInstance);
}