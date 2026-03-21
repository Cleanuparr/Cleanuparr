namespace Cleanuparr.Domain.Entities.Arr;

public sealed record PagedResponse<T>
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalRecords { get; init; }
    public List<T> Records { get; init; } = [];
}
