using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sabnzbd;

/// <summary>
/// The error envelope SABnzbd can return on any call. Read responses omit <c>status</c> on success,
/// so <see cref="Status"/> signals an error when <c>false</c> and nothing otherwise.
/// </summary>
public sealed record SabnzbdActionResponse
{
    [JsonPropertyName("status")]
    public bool? Status { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
