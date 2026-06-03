using Newtonsoft.Json;

namespace Cleanuparr.Domain.Entities.LazyLibrarian;

/// <summary>
/// Represents a row returned by LazyLibrarian's <c>getHistory</c> command.
/// </summary>
public sealed record LazyLibrarianWantedRecord
{
    [JsonProperty("BookID")]
    public string? BookId { get; init; }

    [JsonProperty("NZBtitle")]
    public string? Title { get; init; }

    [JsonProperty("DownloadID")]
    public string? DownloadId { get; init; }

    [JsonProperty("Source")]
    public string? Source { get; init; }

    [JsonProperty("Status")]
    public string? Status { get; init; }

    [JsonProperty("NZBmode")]
    public string? NzbMode { get; init; }
}
