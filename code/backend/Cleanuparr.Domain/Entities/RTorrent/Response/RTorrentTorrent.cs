namespace Cleanuparr.Domain.Entities.RTorrent.Response;

/// <summary>
/// Represents a torrent from rTorrent's XML-RPC multicall response
/// </summary>
public sealed record RTorrentTorrent
{
    /// <summary>
    /// Torrent info hash (40-character hex string, uppercase)
    /// </summary>
    public required string Hash { get; init; }

    /// <summary>
    /// Torrent name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether the torrent is from a private tracker (0 or 1)
    /// </summary>
    public int IsPrivate { get; init; }

    /// <summary>
    /// Total size of the torrent in bytes
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// Number of bytes completed/downloaded
    /// </summary>
    public long CompletedBytes { get; init; }

    /// <summary>
    /// Current download rate in bytes per second
    /// </summary>
    public long DownRate { get; init; }

    /// <summary>
    /// Upload/download ratio multiplied by 1000 (e.g., 1500 = 1.5 ratio)
    /// </summary>
    public long Ratio { get; init; }

    /// <summary>
    /// Torrent state: 0 = stopped, 1 = started
    /// </summary>
    public int State { get; init; }

    /// <summary>
    /// Completion status: 0 = incomplete, 1 = complete
    /// </summary>
    public int Complete { get; init; }

    /// <summary>
    /// Unix timestamp when the torrent finished downloading (0 if not finished)
    /// </summary>
    public long TimestampFinished { get; init; }

    /// <summary>
    /// Label/category from d.custom1 (commonly used by ruTorrent for labels)
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Base path where the torrent data is stored
    /// </summary>
    public string? BasePath { get; init; }

    /// <summary>
    /// List of tracker URLs for this torrent
    /// </summary>
    public IReadOnlyList<string>? Trackers { get; init; }
}
