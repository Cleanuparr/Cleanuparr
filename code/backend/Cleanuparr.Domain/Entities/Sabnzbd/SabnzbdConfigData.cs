using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sabnzbd;

/// <summary>
/// The <c>config</c> payload of SABnzbd's <c>get_config</c> API response.
/// </summary>
public sealed record SabnzbdConfigData
{
    [JsonPropertyName("misc")]
    public SabnzbdMiscConfig? Misc { get; init; }
}
