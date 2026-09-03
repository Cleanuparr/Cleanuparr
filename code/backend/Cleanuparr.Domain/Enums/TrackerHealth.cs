namespace Cleanuparr.Domain.Enums;

/// <summary>
/// What the download client currently knows about a torrent's trackers.
/// </summary>
public enum TrackerHealth
{
    /// <summary>
    /// The client cannot report tracker state.
    /// </summary>
    Unsupported,

    /// <summary>
    /// At least one tracker is answering, so the seeder count is current.
    /// </summary>
    Working,

    /// <summary>
    /// A tracker reports that the torrent is no longer registered.
    /// </summary>
    Unregistered,

    /// <summary>
    /// No tracker is answering, and nothing indicates the torrent itself is gone.
    /// </summary>
    Inconclusive,
}
