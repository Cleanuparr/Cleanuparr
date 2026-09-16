namespace Cleanuparr.Domain.Entities.Arr.History;

/// <summary>
/// One page of the history of an *arr application.
/// </summary>
public sealed record ArrHistoryResponse
{
    /// <summary>
    /// The rows on this page.
    /// </summary>
    public IReadOnlyList<ArrHistoryRecord> Records { get; init; } = [];
}
