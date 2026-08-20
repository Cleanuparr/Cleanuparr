using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.LazyLibrarian;

/// <summary>
/// The payload of a getDownloadProgress response for a single download.
/// </summary>
public sealed record LazyLibrarianDownloadProgress
{
    /// <summary>
    /// -1 when the client no longer holds the download.
    /// -2 when LazyLibrarian could not reach the client.
    /// </summary>
    [JsonPropertyName("progress")]
    public int Progress { get; init; }

    [JsonPropertyName("finished")]
    public bool Finished { get; init; }
}
