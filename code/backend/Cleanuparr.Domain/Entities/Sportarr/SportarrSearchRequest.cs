using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sportarr;

/// <summary>
/// Body of Sportarr's event search request.
/// </summary>
public sealed record SportarrSearchRequest
{
    [JsonPropertyName("eventId")]
    public long EventId { get; init; }
}
