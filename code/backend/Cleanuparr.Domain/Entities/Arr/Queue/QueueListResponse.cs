namespace Cleanuparr.Domain.Entities.Arr.Queue;

/// <summary>
/// One page of the queue of an *arr application.
/// </summary>
public record QueueListResponse
{
    /// <summary>
    /// The number of items in the queue, on this page and on the other pages.
    /// </summary>
    public int TotalRecords { get; init; }

    /// <summary>
    /// The items on this page.
    /// </summary>
    public IReadOnlyList<QueueRecord> Records { get; init; } = [];
}
