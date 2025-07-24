using Newtonsoft.Json;

namespace Cleanuparr.Domain.Entities.UTorrent.Response;

/// <summary>
/// Specific response type for torrent properties API calls
/// Replaces the generic UTorrentResponse<T> for properties retrieval
/// </summary>
public sealed class PropertiesResponse
{
    /// <summary>
    /// Raw properties data from the API
    /// </summary>
    [JsonProperty(PropertyName = "props")]
    public object[]? PropertiesRaw { get; set; }

    /// <summary>
    /// Parsed properties as strongly-typed object
    /// </summary>
    [JsonIgnore]
    public UTorrentProperties Properties { get; set; } = new();
}
