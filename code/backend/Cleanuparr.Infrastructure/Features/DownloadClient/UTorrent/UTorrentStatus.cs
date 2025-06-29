namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// µTorrent status bitfield constants
/// Based on the µTorrent Web UI API documentation
/// </summary>
public static class UTorrentStatus
{
    public const int Started = 1;         // 1 << 0
    public const int Checking = 2;        // 1 << 1
    public const int StartAfterCheck = 4; // 1 << 2
    public const int Checked = 8;         // 1 << 3
    public const int Error = 16;          // 1 << 4
    public const int Paused = 32;         // 1 << 5
    public const int Queued = 64;         // 1 << 6
    public const int Loaded = 128;        // 1 << 7
}