using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sportarr;

public sealed record SportarrSearchRequest
{
    [JsonPropertyName("eventId")]
    public long EventId { get; init; }
}
