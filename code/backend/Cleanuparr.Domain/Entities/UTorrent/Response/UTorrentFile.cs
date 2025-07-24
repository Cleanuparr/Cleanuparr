namespace Cleanuparr.Domain.Entities.UTorrent.Response;

/// <summary>
/// Represents a file within a torrent from µTorrent Web UI API
/// Based on the files array structure from the API documentation
/// </summary>
public sealed class UTorrentFile
{
    public string Name { get; set; } = string.Empty;

    public long Size { get; set; }

    public long Downloaded { get; set; }

    public int Priority { get; set; }

    public int Index { get; set; }
} 