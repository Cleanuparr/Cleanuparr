using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// Wrapper for UTorrent UTorrentItem and UTorrentProperties that implements ITorrentItem interface
/// </summary>
public sealed class UTorrentItemWrapper : ITorrentItem
{
    private readonly UTorrentItem _torrentItem;
    private readonly UTorrentProperties _torrentProperties;

    public UTorrentItemWrapper(UTorrentItem torrentItem, UTorrentProperties torrentProperties)
    {
        _torrentItem = torrentItem ?? throw new ArgumentNullException(nameof(torrentItem));
        _torrentProperties = torrentProperties ?? throw new ArgumentNullException(nameof(torrentProperties));
    }

    // Basic identification
    public string Hash => _torrentItem.Hash;
    public string Name => _torrentItem.Name;

    // Privacy and tracking
    public bool IsPrivate => _torrentProperties.IsPrivate;
    public IReadOnlyList<string> Trackers => _torrentProperties.TrackerList
        .Select(ExtractHostFromUrl)
        .Where(host => !string.IsNullOrEmpty(host))
        .Distinct()
        .ToList()
        .AsReadOnly();

    // Size and progress
    public long Size => _torrentItem.Size;
    public double CompletionPercentage => _torrentItem.Progress / 10.0; // Progress is in permille (1000 = 100%)
    public long DownloadedBytes => _torrentItem.Downloaded;
    public long TotalUploaded => _torrentItem.Uploaded;

    // Speed and transfer rates
    public long DownloadSpeed => _torrentItem.DownloadSpeed;
    public long UploadSpeed => _torrentItem.UploadSpeed;
    public double Ratio => _torrentItem.Ratio;

    // Time tracking
    public long Eta => _torrentItem.ETA;
    public DateTime? DateAdded => _torrentItem.DateAdded > 0
        ? DateTimeOffset.FromUnixTimeSeconds(_torrentItem.DateAdded).DateTime
        : null;
    public DateTime? DateCompleted => _torrentItem.DateCompletedDateTime;
    public long SeedingTimeSeconds => (long?)_torrentItem.SeedingTime?.TotalSeconds ?? 0;

    // Categories and tags
    public string? Category => _torrentItem.Label;
    public IReadOnlyList<string> Tags => Array.Empty<string>(); // uTorrent doesn't have tags

    // State checking methods using status bitfield
    public bool IsDownloading() =>
        (_torrentItem.Status & UTorrentStatus.Started) != 0 &&
        (_torrentItem.Status & UTorrentStatus.Checked) != 0 &&
        (_torrentItem.Status & UTorrentStatus.Error) == 0;

    public bool IsStalled() => IsDownloading() && _torrentItem.DownloadSpeed == 0 && _torrentItem.ETA == 0;

    public bool IsSeeding() => IsDownloading() && _torrentItem.DateCompleted > 0;

    public bool IsCompleted() => _torrentItem.ProgressPercent >= 1.0;

    public bool IsPaused() => (_torrentItem.Status & UTorrentStatus.Paused) != 0;

    public bool IsQueued() => (_torrentItem.Status & UTorrentStatus.Queued) != 0;

    public bool IsChecking() => (_torrentItem.Status & UTorrentStatus.Checking) != 0;

    public bool IsAllocating() => false; // uTorrent doesn't have a specific allocating state

    public bool IsMetadataDownloading() => false; // uTorrent doesn't have this state

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

            if (_torrentProperties.TrackerList.Any(x => x.ShouldIgnore(ignoredDownloads)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts the host from a tracker URL
    /// </summary>
    private static string ExtractHostFromUrl(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return string.Empty;
    }
}
