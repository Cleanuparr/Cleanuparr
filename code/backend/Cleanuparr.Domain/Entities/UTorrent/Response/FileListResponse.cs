using Newtonsoft.Json;

namespace Cleanuparr.Domain.Entities.UTorrent.Response;

/// <summary>
/// Specific response type for file list API calls
/// Replaces the generic UTorrentResponse<T> for file listings
/// </summary>
public sealed class FileListResponse
{
    /// <summary>
    /// Raw file data from the API
    /// </summary>
    [JsonProperty(PropertyName = "files")]
    public object[]? FilesRaw { get; set; }

    /// <summary>
    /// Torrent hash for which files are listed
    /// </summary>
    [JsonIgnore]
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Parsed files as strongly-typed objects
    /// </summary>
    [JsonIgnore]
    public List<UTorrentFile> Files { get; set; } = new();
}
