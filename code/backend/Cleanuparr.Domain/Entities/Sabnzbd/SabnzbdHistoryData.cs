using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sabnzbd;

public sealed record SabnzbdHistoryData
{
    [JsonPropertyName("slots")]
    public IReadOnlyList<SabnzbdHistorySlot> Slots { get; init; } = [];
}
