using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Domain.Entities;

/// <summary>
/// Universal abstraction for a torrent item across all download clients.
/// Provides a unified interface for accessing torrent properties and state.
/// </summary>
public interface ITorrentItemWrapper : IDownloadItem
{
    bool IsPrivate { get; }

    double Ratio { get; }

    /// <summary>
    /// Total seeders reported by the download client when available.
    /// </summary>
    int? SeederCount { get; }

    /// <summary>
    /// Whether a tracker vouches for the torrent right now.
    /// Qualifies <see cref="SeederCount"/>, which clients keep reporting from the last tracker answer they saw.
    /// </summary>
    TrackerHealth TrackerHealth { get; }

    /// <summary>
    /// When the download client added the torrent.
    /// Null when the client reports no added time.
    /// </summary>
    DateTimeOffset? AddedOn { get; }

    long Eta { get; }

    long SeedingTimeSeconds { get; }

    DateTime? LastActivityTime { get; }

    /// <summary>
    /// Tracker domains extracted from all trackers associated with this torrent.
    /// Used for tracker-based seeding rule matching.
    /// </summary>
    IReadOnlyList<string> TrackerDomains { get; }

    /// <summary>
    /// Tags or labels associated with this torrent.
    /// Populated for qBittorrent (tags) and Transmission (labels). Empty for other clients.
    /// </summary>
    IReadOnlyList<string> Tags { get; }

    bool IsStalled();
}
