using System.Text.Json.Serialization;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Domain.Entities.LazyLibrarian;

/// <summary>
/// A row from LazyLibrarian's getHistory command.
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
    public LazyLibrarianSource Source { get; init; }

    [JsonPropertyName("Status")]
    public LazyLibrarianStatus Status { get; init; }

    /// <summary>
    /// The provider type, not the protocol.
    /// </summary>
    [JsonPropertyName("NZBmode")]
    public LazyLibrarianDownloadMode Mode { get; init; }

    /// <summary>
    /// A magazine row carries an issue date here, so it reads as Unknown.
    /// </summary>
    [JsonPropertyName("AuxInfo")]
    public BookLibrary Library { get; init; }

    [JsonPropertyName("Origin")]
    public LazyLibrarianOrigin Origin { get; init; }
}
