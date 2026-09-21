namespace Cleanuparr.Domain.Entities.Arr.History;

/// <summary>
/// What the history of an *arr application holds for one query.
/// </summary>
public sealed record ArrHistoryResponse
{
    /// <summary>
    /// How many rows match the query, on this page and on the other pages.
    /// </summary>
    public int TotalRecords { get; init; }
}
