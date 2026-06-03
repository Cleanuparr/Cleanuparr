using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.LazyLibrarian;

/// <summary>
/// Represents a row returned by LazyLibrarian's <c>getHistory</c> command.
/// </summary>
public sealed record LazyLibrarianWantedRecord
{
    [JsonPropertyName("BookID")]
    public string? BookId { get; init; }

    [JsonPropertyName("NZBtitle")]
    public string? Title { get; init; }

    [JsonPropertyName("DownloadID")]
    public string? DownloadId { get; init; }

    [JsonPropertyName("Source")]
    public string? Source { get; init; }

    [JsonPropertyName("Status")]
    public string? Status { get; init; }

    [JsonPropertyName("NZBmode")]
    public string? NzbMode { get; init; }
}
