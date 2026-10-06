using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sabnzbd;

public sealed record SabnzbdQueueResponse
{
    [JsonPropertyName("queue")]
    public SabnzbdQueueData? Queue { get; init; }
}
