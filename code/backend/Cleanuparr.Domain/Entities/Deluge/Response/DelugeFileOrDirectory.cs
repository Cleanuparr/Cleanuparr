using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Deluge.Response;

/// <summary>
/// One node in the file tree of a torrent in Deluge.
/// A node is a file or a directory, and the value of <see cref="Type"/> tells which one it is.
/// Deluge sends the file fields only on a file node.
/// </summary>
public class DelugeFileOrDirectory
{
    /// <summary>
    /// The type of the node.
    /// The value is "file" or "dir".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The child nodes of a directory node.
    /// </summary>
    [JsonPropertyName("contents")]
    public Dictionary<string, DelugeFileOrDirectory>? Contents { get; set; }

    /// <summary>
    /// The position of the file in the list of files of the torrent.
    /// Deluge sends this field only on a file node, and the value is 0 on a directory node.
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>
    /// The path of the node in the torrent.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The size of the node in bytes.
    /// A file larger than 2 GiB makes this value too large for a 32-bit integer.
    /// </summary>
    [JsonPropertyName("size")]
    public long? Size { get; set; }

    /// <summary>
    /// The offset of the file in the data of the torrent.
    /// </summary>
    [JsonPropertyName("offset")]
    public long? Offset { get; set; }

    /// <summary>
    /// The part of the node that Deluge has, as a value from 0 to 100.
    /// </summary>
    [JsonPropertyName("progress")]
    public double? Progress { get; set; }

    /// <summary>
    /// The download priority of the node.
    /// A value of 0 tells Deluge to skip the file.
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    /// <summary>
    /// The progress of each file below a directory node.
    /// </summary>
    [JsonPropertyName("progresses")]
    public List<double> Progresses { get; set; } = [];
}
