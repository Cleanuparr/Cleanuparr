using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sabnzbd;

/// <summary>
/// The response for SABnzbd's <c>mode=get_config</c> API call.
/// </summary>
public sealed record SabnzbdGetConfigResponse
{
    [JsonPropertyName("config")]
    public SabnzbdConfigData? Config { get; init; }
}
