using System.Text.Json.Serialization;

namespace Cleanuparr.Api.Contracts.Responses;

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    [JsonIgnore]
    public bool HasPrevious => Page > 1;

    [JsonIgnore]
    public bool HasNext => Page < TotalPages;
}
