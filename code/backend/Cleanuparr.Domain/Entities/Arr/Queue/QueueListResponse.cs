namespace Cleanuparr.Domain.Entities.Arr.Queue;

public record QueueListResponse
{
    public int TotalRecords { get; init; }
    public IReadOnlyList<QueueRecord> Records { get; init; } = [];
}