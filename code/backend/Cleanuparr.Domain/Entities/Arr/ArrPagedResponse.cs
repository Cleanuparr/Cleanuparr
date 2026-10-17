namespace Cleanuparr.Domain.Entities.Arr;

/// <summary>
/// One page of a paged *arr list response (e.g. wanted/missing, wanted/cutoff).
/// </summary>
public record ArrPagedResponse<T>
{
    /// <summary>
    /// The number of items in the list, on this page and on the other pages.
    /// </summary>
    public int TotalRecords { get; init; }

    /// <summary>
    /// The items on this page.
    /// </summary>
    public IReadOnlyList<T> Records { get; init; } = [];
}
