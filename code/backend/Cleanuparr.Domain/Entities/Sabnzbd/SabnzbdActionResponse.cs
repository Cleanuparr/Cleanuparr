using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sabnzbd;

/// <summary>
/// The envelope SABnzbd returns for an action call (delete, pause, change_cat, ...).
/// </summary>
public sealed record SabnzbdActionResponse
{
    [JsonPropertyName("status")]
    public bool Status { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
