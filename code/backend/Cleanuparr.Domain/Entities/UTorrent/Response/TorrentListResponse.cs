using Newtonsoft.Json;

namespace Cleanuparr.Domain.Entities.UTorrent.Response;

/// <summary>
/// Specific response type for torrent list API calls
/// Replaces the generic UTorrentResponse<T> for torrent listings
/// </summary>
public sealed class TorrentListResponse
{
    /// <summary>
    /// µTorrent build number
    /// </summary>
    [JsonProperty(PropertyName = "build")]
    public int Build { get; set; }

    /// <summary>
    /// List of torrent data from the API
    /// </summary>
    [JsonProperty(PropertyName = "torrents")]
    public object[][]? TorrentsRaw { get; set; }

    /// <summary>
    /// Label data from the API
    /// </summary>
    [JsonProperty(PropertyName = "label")]
    public object[][]? LabelsRaw { get; set; }

    /// <summary>
    /// Parsed torrents as strongly-typed objects
    /// </summary>
    [JsonIgnore]
    public List<UTorrentItem> Torrents { get; set; } = new();

    /// <summary>
    /// Parsed labels as string list
    /// </summary>
    [JsonIgnore]
    public List<string> Labels { get; set; } = new();
}
