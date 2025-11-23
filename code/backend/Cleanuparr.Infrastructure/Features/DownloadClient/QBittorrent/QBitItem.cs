using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

/// <summary>
/// Wrapper for QBittorrent TorrentInfo that implements ITorrentItem interface
/// </summary>
public sealed class QBitItem : ITorrentItem
{
    private readonly TorrentInfo _torrentInfo;
    private readonly IReadOnlyList<TorrentTracker> _trackers;
    private readonly bool _isPrivate;

    public QBitItem(TorrentInfo torrentInfo, IReadOnlyList<TorrentTracker> trackers, bool isPrivate)
    {
        _torrentInfo = torrentInfo ?? throw new ArgumentNullException(nameof(torrentInfo));
        _trackers = trackers ?? throw new ArgumentNullException(nameof(trackers));
        _isPrivate = isPrivate;
    }

    // Basic identification
    public string Hash => _torrentInfo.Hash ?? string.Empty;
    public string Name => _torrentInfo.Name ?? string.Empty;

    // Privacy and tracking
    public bool IsPrivate => _isPrivate;
    public IReadOnlyList<string> Trackers => _trackers
        .Where(t => !string.IsNullOrEmpty(t.Url))
        .Select(t => ExtractHostFromUrl(t.Url!))
        .Where(host => !string.IsNullOrEmpty(host))
        .Distinct()
        .ToList()
        .AsReadOnly();

    // Size and progress
    public long Size => _torrentInfo.Size;
    public double CompletionPercentage => _torrentInfo.Progress * 100.0;
    public long DownloadedBytes => _torrentInfo.Downloaded ?? 0;
    public long TotalUploaded => _torrentInfo.Uploaded ?? 0;

    // Speed and transfer rates
    public long DownloadSpeed => _torrentInfo.DownloadSpeed;
    public long UploadSpeed => _torrentInfo.UploadSpeed;
    public double Ratio => _torrentInfo.Ratio;

    // Time tracking
    public long Eta => _torrentInfo.EstimatedTime?.TotalSeconds is double eta ? (long)eta : 0;
    public DateTime? DateAdded => _torrentInfo.AddedOn;
    public DateTime? DateCompleted => _torrentInfo.CompletionOn;
    public long SeedingTimeSeconds => _torrentInfo.SeedingTime?.TotalSeconds is double seedTime ? (long)seedTime : 0;

    // Categories and tags
    public string? Category => _torrentInfo.Category;
    public IReadOnlyList<string> Tags => _torrentInfo.Tags?.ToList().AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();

    // State checking methods
    public bool IsDownloading() => _torrentInfo.State is TorrentState.Downloading or TorrentState.ForcedDownload;
    public bool IsStalled() => _torrentInfo.State is TorrentState.StalledDownload;
    public bool IsSeeding() => _torrentInfo.State is TorrentState.Uploading or TorrentState.ForcedUpload or TorrentState.StalledUpload;
    public bool IsCompleted() => CompletionPercentage >= 100.0;
    public bool IsPaused() => _torrentInfo.State is TorrentState.PausedDownload or TorrentState.PausedUpload;
    public bool IsQueued() => _torrentInfo.State is TorrentState.QueuedDownload or TorrentState.QueuedUpload;
    public bool IsChecking() => _torrentInfo.State is TorrentState.CheckingDownload or TorrentState.CheckingUpload or TorrentState.CheckingResumeData;
    public bool IsAllocating() => _torrentInfo.State is TorrentState.Allocating;
    public bool IsMetadataDownloading() => _torrentInfo.State is TorrentState.FetchingMetadata or TorrentState.ForcedFetchingMetadata;

    // Filtering methods
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

            if (_torrentInfo.Tags?.Contains(pattern, StringComparer.InvariantCultureIgnoreCase) is true)
            {
                return true;
            }
            
            if (_trackers.Any(tracker => tracker.ShouldIgnore(ignoredDownloads)))
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
