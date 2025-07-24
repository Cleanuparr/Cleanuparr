using Newtonsoft.Json;

namespace Cleanuparr.Domain.Entities.UTorrent.Response;

/// <summary>
/// Represents a torrent from µTorrent Web UI API
/// Based on the torrent array structure from the API documentation
/// </summary>
public sealed class UTorrentItem
{
    /// <summary>
    /// Torrent hash (index 0)
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Status bitfield (index 1)
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Torrent name (index 2)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Total size in bytes (index 3)
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Progress in permille (1000 = 100%) (index 4)
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Downloaded bytes (index 5)
    /// </summary>
    public long Downloaded { get; set; }

    /// <summary>
    /// Uploaded bytes (index 6)
    /// </summary>
    public long Uploaded { get; set; }

    /// <summary>
    /// Ratio * 1000 (index 7)
    /// </summary>
    public int RatioRaw { get; set; }

    /// <summary>
    /// Upload speed in bytes/sec (index 8)
    /// </summary>
    public int UploadSpeed { get; set; }

    /// <summary>
    /// Download speed in bytes/sec (index 9)
    /// </summary>
    public int DownloadSpeed { get; set; }

    /// <summary>
    /// ETA in seconds (index 10)
    /// </summary>
    public int ETA { get; set; }

    /// <summary>
    /// Label (index 11)
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Connected peers (index 12)
    /// </summary>
    public int PeersConnected { get; set; }

    /// <summary>
    /// Peers in swarm (index 13)
    /// </summary>
    public int PeersInSwarm { get; set; }

    /// <summary>
    /// Connected seeds (index 14)
    /// </summary>
    public int SeedsConnected { get; set; }

    /// <summary>
    /// Seeds in swarm (index 15)
    /// </summary>
    public int SeedsInSwarm { get; set; }

    /// <summary>
    /// Availability (index 16)
    /// </summary>
    public int Availability { get; set; }

    /// <summary>
    /// Queue order (index 17)
    /// </summary>
    public int QueueOrder { get; set; }

    /// <summary>
    /// Remaining bytes (index 18)
    /// </summary>
    public long Remaining { get; set; }

    /// <summary>
    /// Download URL (index 19)
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// RSS feed URL (index 20)
    /// </summary>
    public string RssFeedUrl { get; set; } = string.Empty;

    /// <summary>
    /// Status message (index 21)
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Stream ID (index 22)
    /// </summary>
    public string StreamId { get; set; } = string.Empty;

    /// <summary>
    /// Date added as Unix timestamp (index 23)
    /// </summary>
    public long DateAdded { get; set; }

    /// <summary>
    /// Date completed as Unix timestamp (index 24)
    /// </summary>
    public long DateCompleted { get; set; }

    /// <summary>
    /// App update URL (index 25)
    /// </summary>
    public string AppUpdateUrl { get; set; } = string.Empty;

    /// <summary>
    /// Save path (index 26)
    /// </summary>
    public string SavePath { get; set; } = string.Empty;

    /// <summary>
    /// Calculated ratio value (RatioRaw / 1000.0)
    /// </summary>
    [JsonIgnore]
    public double Ratio => RatioRaw / 1000.0;

    /// <summary>
    /// Progress as percentage (0.0 to 1.0)
    /// </summary>
    [JsonIgnore]
    public double ProgressPercent => Progress / 1000.0;

    /// <summary>
    /// Date completed as DateTime (or null if not completed)
    /// </summary>
    [JsonIgnore]
    public DateTime? DateCompletedDateTime => 
        DateCompleted > 0 ? DateTimeOffset.FromUnixTimeSeconds(DateCompleted).DateTime : null;

    /// <summary>
    /// Seeding time in seconds (calculated from DateCompleted to now)
    /// </summary>
    [JsonIgnore]
    public TimeSpan? SeedingTime
    {
        get
        {
            if (DateCompletedDateTime.HasValue)
            {
                return DateTime.UtcNow - DateCompletedDateTime.Value;
            }
            
            return null;
        }
    }
} 