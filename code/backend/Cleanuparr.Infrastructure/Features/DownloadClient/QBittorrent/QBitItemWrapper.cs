using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using Cleanuparr.Infrastructure.Services;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

/// <summary>
/// Wrapper for QBittorrent TorrentInfo that implements ITorrentItem interface
/// </summary>
public sealed class QBitItemWrapper : ITorrentItemWrapper
{
    private readonly IReadOnlyList<TorrentTracker> _trackers;
    private readonly Lazy<IReadOnlyList<string>> _trackerDomains;

    public TorrentInfo Info { get; }

    public QBitItemWrapper(TorrentInfo torrentInfo, IReadOnlyList<TorrentTracker> trackers, bool isPrivate)
    {
        Info = torrentInfo ?? throw new ArgumentNullException(nameof(torrentInfo));
        _trackers = trackers ?? throw new ArgumentNullException(nameof(trackers));
        IsPrivate = isPrivate;
        _trackerDomains = new Lazy<IReadOnlyList<string>>(() => _trackers
            .Select(t => UriService.GetDomain(t.Url))
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList()
            .AsReadOnly());
    }

    public string Hash => Info.Hash ?? string.Empty;
    
    public string Name => Info.Name ?? string.Empty;

    public bool IsPrivate { get; }

    public long Size => Info.Size;
    
    public double CompletionPercentage => Info.Progress * 100.0;
    
    public long DownloadedBytes => Info.Downloaded ?? 0;

    public long DownloadSpeed => Info.DownloadSpeed;
    
    public double Ratio => Info.Ratio;

    /// <inheritdoc/>
    public int? SeederCount => Info.TotalSeeds;

    /// <inheritdoc/>
    public TrackerHealth TrackerHealth
    {
        get
        {
            // qBittorrent lists "** [DHT] **" as a Working tracker, and one leaked pseudo-row would mask a dead tracker.
            List<TorrentTracker> realTrackers = _trackers
                .Where(tracker => tracker.Url?.Contains("**") is not true)
                .ToList();

            if (realTrackers.Count == 0)
            {
                return TrackerHealth.Unsupported;
            }

            // No status number changes verdict class across versions, so the client version does not matter here:
            // 3 (updating) exists only on 4.6 to 5.1.
            // 5 and 6 exist only on 5.2+.
            // 4 means failed on both.
            if (realTrackers.Any(tracker => (int?)tracker.TrackerStatus is 2))
            {
                return TrackerHealth.Working;
            }

            if (realTrackers.Any(tracker => (int?)tracker.TrackerStatus is 1 or 3))
            {
                return TrackerHealth.Inconclusive;
            }

            List<TorrentTracker> failing = realTrackers
                .Where(tracker => (int?)tracker.TrackerStatus is 4 or 5 or 6)
                .ToList();

            if (failing.Count == 0)
            {
                return TrackerHealth.Unsupported;
            }

            if (failing.Any(tracker => TrackerMessageClassifier.Classify(tracker.Message) is TrackerHealth.Unregistered))
            {
                return TrackerHealth.Unregistered;
            }

            return TrackerHealth.Inconclusive;
        }
    }

    /// <inheritdoc/>
    /// <remarks>qBittorrent sends <c>added_on</c> as a UTC instant that deserializes with an unspecified kind.</remarks>
    public DateTimeOffset? AddedOn => Info.AddedOn is { } addedOn
        ? new DateTimeOffset(addedOn, TimeSpan.Zero)
        : null;

    public long Eta => Info.EstimatedTime?.TotalSeconds is { } eta ? (long)eta : 0;
    
    public long SeedingTimeSeconds => Info.SeedingTime?.TotalSeconds is { } seedTime ? (long)seedTime : 0;

    public DateTime? LastActivityTime => Info.LastActivityTime;

    public string? Category
    {
        get => Info.Category;
        set => Info.Category = value;
    }

    public string SavePath => Info.SavePath ?? string.Empty;

    public IReadOnlyList<string> TrackerDomains => _trackerDomains.Value;

    public IReadOnlyList<string> Tags => Info.Tags?.ToList().AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();

    /// <inheritdoc/>
    public bool IsStopped => Info.State is TorrentState.PausedUpload or TorrentState.PausedDownload;

    public bool IsDownloading() => Info.State is TorrentState.Downloading or TorrentState.ForcedDownload;
    
    public bool IsStalled() => Info.State is TorrentState.StalledDownload;
    
    public bool IsSeeding() => Info.State is TorrentState.Uploading or TorrentState.ForcedUpload or TorrentState.StalledUpload;
    
    public bool IsMetadataDownloading() => Info.State is TorrentState.FetchingMetadata or TorrentState.ForcedFetchingMetadata;

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

            if (Info.Tags?.Contains(pattern, StringComparer.InvariantCultureIgnoreCase) is true)
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
}
