using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.UTorrent.Response;

/// <summary>
/// Specific response type for label list API calls
/// Replaces the generic UTorrentResponse<T> for label listings
/// </summary>
public sealed class LabelListResponse
{
    /// <summary>
    /// Raw label data from the API
    /// </summary>
    [JsonPropertyName("label")]
    public JsonElement[][]? LabelsRaw { get; set; }

    /// <summary>
    /// Parsed labels as string list
    /// </summary>
    [JsonIgnore]
    public List<string> Labels { get; set; } = new();
}
