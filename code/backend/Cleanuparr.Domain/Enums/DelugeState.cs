using Newtonsoft.Json;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Torrent state values reported by Deluge
/// </summary>
[JsonConverter(typeof(DelugeStateConverter))]
public enum DelugeState
{
    Unknown = 0,
    Allocating,
    Checking,
    Downloading,
    Seeding,
    Paused,
    Error,
    Queued,
    Moving,
}
