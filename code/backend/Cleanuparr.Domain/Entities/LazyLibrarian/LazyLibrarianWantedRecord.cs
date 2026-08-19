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

    /// <summary>
    /// The library the row belongs to.
    /// A book row reads eBook or AudioBook.
    /// A comic row reads comic and a magazine row reads an issue date.
    /// </summary>
    [JsonPropertyName("AuxInfo")]
    public string? AuxInfo { get; init; }

    /// <summary>
    /// Whether LazyLibrarian created the download task itself.
    /// It writes "new" for its own task.
    /// It writes "adopted" for one the client already held.
    /// </summary>
    [JsonPropertyName("Origin")]
    public string? Origin { get; init; }
}
