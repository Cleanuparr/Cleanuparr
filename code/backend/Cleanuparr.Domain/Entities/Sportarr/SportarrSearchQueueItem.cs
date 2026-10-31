namespace Cleanuparr.Domain.Entities.Sportarr;

/// <summary>
/// What Sportarr answers when an event search is queued.
/// </summary>
public sealed record SportarrSearchQueueItem
{
    /// <summary>
    /// The id of the task that tracks the search.
    /// Sportarr drops the search item itself minutes after it ends, so this id is the lasting handle.
    /// </summary>
    public long? TaskId { get; init; }
}
