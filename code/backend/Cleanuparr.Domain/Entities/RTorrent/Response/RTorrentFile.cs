namespace Cleanuparr.Domain.Entities.RTorrent.Response;

/// <summary>
/// Represents a file within a torrent from rTorrent's XML-RPC f.multicall response
/// </summary>
public sealed record RTorrentFile
{
    /// <summary>
    /// File index within the torrent (0-based)
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// File path relative to the torrent base directory
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// Download priority: 0 = skip/don't download, 1 = normal, 2 = high
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Number of completed chunks for this file
    /// </summary>
    public long CompletedChunks { get; init; }

    /// <summary>
    /// Total number of chunks for this file
    /// </summary>
    public long SizeChunks { get; init; }
}
