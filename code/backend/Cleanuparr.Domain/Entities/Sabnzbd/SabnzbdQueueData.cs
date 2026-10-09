using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sabnzbd;

/// <summary>
/// The SABnzbd queue. Download speed is reported for the server as a whole, not per job.
/// </summary>
public sealed record SabnzbdQueueData
{
    [JsonPropertyName("kbpersec")]
    public double KbPerSec { get; init; }

    [JsonPropertyName("paused")]
    public bool Paused { get; init; }

    [JsonPropertyName("slots")]
    public IReadOnlyList<SabnzbdQueueSlot> Slots { get; init; } = [];
}
