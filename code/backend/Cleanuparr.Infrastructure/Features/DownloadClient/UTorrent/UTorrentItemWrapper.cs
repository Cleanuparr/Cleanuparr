using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// Wrapper for UTorrent UTorrentItem and UTorrentProperties that implements ITorrentItem interface
/// </summary>
public sealed class UTorrentItemWrapper : ITorrentItemWrapper
{
    public UTorrentItem Info { get; }

    public UTorrentProperties Properties { get; }

    public UTorrentItemWrapper(UTorrentItem torrentItem, UTorrentProperties torrentProperties)
    {
        Info = torrentItem ?? throw new ArgumentNullException(nameof(torrentItem));
        Properties = torrentProperties ?? throw new ArgumentNullException(nameof(torrentProperties));
    }

    // Basic identification
    public string Hash => Info.Hash;
    public string Name => Info.Name;

    // Privacy and tracking
    public bool IsPrivate => Properties.IsPrivate;

    // Size and progress
    public long Size => Info.Size;
    public double CompletionPercentage => Info.Progress / 10.0; // Progress is in permille (1000 = 100%)
    public long DownloadedBytes => Info.Downloaded;

    // Speed and transfer rates
    public long DownloadSpeed => Info.DownloadSpeed;
    public double Ratio => Info.Ratio;

    // Time tracking
    public long Eta => Info.ETA;
    public long SeedingTimeSeconds => (long?)Info.SeedingTime?.TotalSeconds ?? 0;

    // Categories and tags
    public string? Category => Info.Label;

    // State checking methods using status bitfield
    public bool IsDownloading() =>
        (Info.Status & UTorrentStatus.Started) != 0 &&
        (Info.Status & UTorrentStatus.Checked) != 0 &&
        (Info.Status & UTorrentStatus.Error) == 0;

    public bool IsStalled() => IsDownloading() && Info.DownloadSpeed == 0 && Info.ETA == 0;

    public bool IsSeeding() => IsDownloading() && Info.DateCompleted > 0;

    public bool IsCompleted() => Info.ProgressPercent >= 1.0;

    public bool IsPaused() => (Info.Status & UTorrentStatus.Paused) != 0;

    public bool IsQueued() => (Info.Status & UTorrentStatus.Queued) != 0;

    public bool IsChecking() => (Info.Status & UTorrentStatus.Checking) != 0;

    // Filtering methods
    public bool IsIgnored(IReadOnlyList<string> ignoredDownloads)
    {
        if (ignoredDownloads.Count == 0)
        {
            return false;
        }
        
        foreach (string value in ignoredDownloads)
        {
            if (Hash.Equals(value, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            
            if (Category?.Equals(value, StringComparison.InvariantCultureIgnoreCase) is true)
            {
                return true;
            }

            if (Properties.TrackerList.Any(x => x.ShouldIgnore(ignoredDownloads)))
            {
                return true;
            }
        }

        return false;
    }
}
