namespace Cleanuparr.Domain.Entities.UTorrent.Response;

/// <summary>
/// Represents torrent properties from µTorrent Web UI API getprops action
/// Based on the properties structure from the API documentation
/// </summary>
public sealed class UTorrentProperties
{
    /// <summary>
    /// Torrent hash
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Trackers list (newlines are represented by \r\n)
    /// </summary>
    public string Trackers { get; set; } = string.Empty;
    
    public List<string> TrackerList => Trackers
        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim())
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToList();

    /// <summary>
    /// Upload limit in bytes per second
    /// </summary>
    public int UploadLimit { get; set; }

    /// <summary>
    /// Download limit in bytes per second
    /// </summary>
    public int DownloadLimit { get; set; }

    /// <summary>
    /// Initial seeding / Super seeding
    /// -1 = Not allowed, 0 = Disabled, 1 = Enabled
    /// </summary>
    public int SuperSeed { get; set; }

    /// <summary>
    /// Use DHT
    /// -1 = Not allowed, 0 = Disabled, 1 = Enabled
    /// </summary>
    public int Dht { get; set; }

    /// <summary>
    /// Use PEX (Peer Exchange)
    /// -1 = Not allowed (indicates private torrent), 0 = Disabled, 1 = Enabled
    /// </summary>
    public int Pex { get; set; }

    /// <summary>
    /// Override queueing
    /// -1 = Not allowed, 0 = Disabled, 1 = Enabled
    /// </summary>
    public int SeedOverride { get; set; }

    /// <summary>
    /// Seed ratio in per mils (1000 = 1.0 ratio)
    /// </summary>
    public int SeedRatio { get; set; }

    /// <summary>
    /// Seeding time in seconds
    /// 0 = No minimum seeding time
    /// </summary>
    public int SeedTime { get; set; }

    /// <summary>
    /// Upload slots
    /// </summary>
    public int UploadSlots { get; set; }

    /// <summary>
    /// Whether this torrent is private (based on PEX value)
    /// Private torrents have PEX = -1 (not allowed)
    /// </summary>
    public bool IsPrivate => Pex == -1;

    /// <summary>
    /// Calculated seed ratio value (SeedRatio / 1000.0)
    /// </summary>
    public double SeedRatioValue => SeedRatio / 1000.0;
} 