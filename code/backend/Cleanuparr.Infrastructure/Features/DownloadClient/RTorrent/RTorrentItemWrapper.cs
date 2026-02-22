using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.RTorrent.Response;
using Cleanuparr.Infrastructure.Services;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;

/// <summary>
/// Wrapper for RTorrentTorrent that implements ITorrentItemWrapper interface
/// </summary>
public sealed class RTorrentItemWrapper : ITorrentItemWrapper
{
    public RTorrentTorrent Info { get; }
    private readonly IReadOnlyList<string> _trackers;
    private string? _category;

    public RTorrentItemWrapper(RTorrentTorrent torrent, IReadOnlyList<string>? trackers = null)
    {
        Info = torrent ?? throw new ArgumentNullException(nameof(torrent));
        _trackers = trackers ?? torrent.Trackers ?? [];
        _category = torrent.Label;
    }

    public string Hash => Info.Hash;

    public string Name => Info.Name;

    public bool IsPrivate => Info.IsPrivate == 1;

    public long Size => Info.SizeBytes;

    public double CompletionPercentage => Info.SizeBytes > 0
        ? (Info.CompletedBytes / (double)Info.SizeBytes) * 100.0
        : 0.0;

    public long DownloadedBytes => Info.CompletedBytes;

    public long DownloadSpeed => Info.DownRate;

    /// <summary>
    /// Ratio from rTorrent (returned as ratio * 1000, so divide by 1000)
    /// </summary>
    public double Ratio => Info.Ratio / 1000.0;

    public long Eta => CalculateEta();

    public long SeedingTimeSeconds => CalculateSeedingTime();

    public string? Category
    {
        get => _category;
        set => _category = value;
    }

    public string SavePath => Info.BasePath ?? string.Empty;

    /// <summary>
    /// Downloading when state is 1 (started) and complete is 0 (not finished)
    /// </summary>
    public bool IsDownloading() => Info.State == 1 && Info.Complete == 0;

    /// <summary>
    /// Stalled when downloading but no download speed and no ETA
    /// </summary>
    public bool IsStalled() => IsDownloading() && Info.DownRate <= 0 && Eta <= 0;

    public bool IsIgnored(IReadOnlyList<string> ignoredDownloads)
    {
        if (ignoredDownloads.Count == 0)
        {
            return false;
        }

        foreach (string pattern in ignoredDownloads)
        {
            if (Hash.Equals(pattern, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (Category?.Equals(pattern, StringComparison.InvariantCultureIgnoreCase) is true)
            {
                return true;
            }

            if (_trackers.Any(url => UriService.GetDomain(url)?.EndsWith(pattern, StringComparison.InvariantCultureIgnoreCase) is true))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Calculate ETA based on remaining bytes and download speed
    /// </summary>
    private long CalculateEta()
    {
        if (Info.DownRate <= 0) return 0;
        long remaining = Info.SizeBytes - Info.CompletedBytes;
        if (remaining <= 0) return 0;
        return remaining / Info.DownRate;
    }

    /// <summary>
    /// Calculate seeding time based on the timestamp when the torrent finished downloading.
    /// rTorrent doesn't natively track seeding time, so we calculate it from completion timestamp.
    /// </summary>
    private long CalculateSeedingTime()
    {
        // If not finished yet, no seeding time
        if (Info.Complete != 1 || Info.TimestampFinished <= 0)
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var seedingTime = now - Info.TimestampFinished;
        return seedingTime > 0 ? seedingTime : 0;
    }
}
