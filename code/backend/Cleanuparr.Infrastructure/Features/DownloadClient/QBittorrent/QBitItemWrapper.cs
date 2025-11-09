using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

/// <summary>
/// Wrapper for QBittorrent TorrentInfo that implements ITorrentItem interface
/// </summary>
public sealed class QBitItemWrapper : ITorrentItemWrapper
{
    private readonly IReadOnlyList<TorrentTracker> _trackers;

    public TorrentInfo Info { get; }

    public QBitItemWrapper(TorrentInfo torrentInfo, IReadOnlyList<TorrentTracker> trackers, bool isPrivate)
    {
        Info = torrentInfo ?? throw new ArgumentNullException(nameof(torrentInfo));
        _trackers = trackers ?? throw new ArgumentNullException(nameof(trackers));
        IsPrivate = isPrivate;
    }

    // Basic identification
    public string Hash => Info.Hash ?? string.Empty;
    public string Name => Info.Name ?? string.Empty;

    // Privacy and tracking
    public bool IsPrivate { get; }

    public IReadOnlyList<string> Trackers => _trackers
        .Where(t => !string.IsNullOrEmpty(t.Url))
        .Select(t => ExtractHostFromUrl(t.Url!))
        .Where(host => !string.IsNullOrEmpty(host))
        .Distinct()
        .ToList()
        .AsReadOnly();

    // Size and progress
    public long Size => Info.Size;
    public double CompletionPercentage => Info.Progress * 100.0;
    public long DownloadedBytes => Info.Downloaded ?? 0;
    public long TotalUploaded => Info.Uploaded ?? 0;

    // Speed and transfer rates
    public long DownloadSpeed => Info.DownloadSpeed;
    public long UploadSpeed => Info.UploadSpeed;
    public double Ratio => Info.Ratio;

    // Time tracking
    public long Eta => Info.EstimatedTime?.TotalSeconds is double eta ? (long)eta : 0;
    public DateTime? DateAdded => Info.AddedOn;
    public DateTime? DateCompleted => Info.CompletionOn;
    public long SeedingTimeSeconds => Info.SeedingTime?.TotalSeconds is double seedTime ? (long)seedTime : 0;

    // Categories and tags
    public string? Category => Info.Category;
    public IReadOnlyList<string> Tags => Info.Tags?.ToList().AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();

    // State checking methods
    public bool IsDownloading() => Info.State is TorrentState.Downloading or TorrentState.ForcedDownload;
    public bool IsStalled() => Info.State is TorrentState.StalledDownload;
    public bool IsSeeding() => Info.State is TorrentState.Uploading or TorrentState.ForcedUpload or TorrentState.StalledUpload;
    public bool IsCompleted() => CompletionPercentage >= 100.0;
    public bool IsPaused() => Info.State is TorrentState.PausedDownload or TorrentState.PausedUpload;
    public bool IsQueued() => Info.State is TorrentState.QueuedDownload or TorrentState.QueuedUpload;
    public bool IsChecking() => Info.State is TorrentState.CheckingDownload or TorrentState.CheckingUpload or TorrentState.CheckingResumeData;
    public bool IsAllocating() => Info.State is TorrentState.Allocating;
    public bool IsMetadataDownloading() => Info.State is TorrentState.FetchingMetadata or TorrentState.ForcedFetchingMetadata;

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

            if (Info.Tags.Contains(pattern, StringComparer.InvariantCultureIgnoreCase))
            {
                return true;
            }
            
            if (Trackers.Any(tracker => tracker.ShouldIgnore(ignoredDownloads)))
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
