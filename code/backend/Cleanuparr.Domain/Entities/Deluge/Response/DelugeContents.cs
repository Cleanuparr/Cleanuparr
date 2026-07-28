using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Deluge.Response;

/// <summary>
/// The root of the file tree of a torrent in Deluge.
/// </summary>
public sealed record DelugeContents
{
    /// <summary>
    /// The child nodes of the root.
    /// </summary>
    [JsonPropertyName("contents")]
    public Dictionary<string, DelugeFileOrDirectory>? Contents { get; set; }

    /// <summary>
    /// The type of the root.
    /// The value is always "dir".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}