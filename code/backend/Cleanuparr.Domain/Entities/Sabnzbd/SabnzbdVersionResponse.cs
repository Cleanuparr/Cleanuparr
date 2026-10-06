using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sabnzbd;

public sealed record SabnzbdVersionResponse
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }
}
