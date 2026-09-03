using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Services;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

/// <summary>
/// Wrapper for Transmission TorrentInfo that implements ITorrentItem interface
/// </summary>
public sealed class TransmissionItemWrapper : ITorrentItemWrapper
{
    private readonly Lazy<IReadOnlyList<string>> _trackerDomains;

    public TorrentInfo Info { get; }

    public TransmissionItemWrapper(TorrentInfo torrentInfo)
    {
        Info = torrentInfo ?? throw new ArgumentNullException(nameof(torrentInfo));
        _trackerDomains = new Lazy<IReadOnlyList<string>>(() => Info.Trackers?
            .Select(t => UriService.GetDomain(t.Announce))
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList()
            .AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>());
    }

    public string Hash => Info.HashString ?? string.Empty;
    
    public string Name => Info.Name ?? string.Empty;

    public bool IsPrivate => Info.IsPrivate ?? false;

    public long Size => Info.TotalSize ?? 0;
    
    public double CompletionPercentage => Info.TotalSize > 0
        ? ((Info.DownloadedEver ?? 0) / (double)Info.TotalSize) * 100.0
        : 0.0;
    
    public long DownloadedBytes => Info.DownloadedEver ?? 0;

    public long DownloadSpeed => Info.RateDownload ?? 0;
    
    public double Ratio => Info.uploadRatio ?? 0.0;

    /// <inheritdoc/>
    /// <remarks>Returns the maximum <c>SeederCount</c> across all tracker stats, or <see langword="null"/> when tracker stats are missing/unscraped.</remarks>
    public int? SeederCount
    {
        get
        {
            if (Info.TrackerStats is not { Length: > 0 } trackerStats)
            {
                return null;
            }

            long? max = trackerStats.Max(t => t.SeederCount);
            return max is >= 0 ? checked((int)max.Value) : null;
        }
    }

    /// <inheritdoc/>
    public TrackerHealth TrackerHealth
    {
        get
        {
            if (Info.TrackerStats is not { Length: > 0 } trackerStats)
            {
                return TrackerHealth.Unsupported;
            }

            // Transmission pins backup entries at announce state INACTIVE, so only the live entry of a tier carries usable state.
            List<TransmissionTorrentTrackerStats> active = trackerStats
                .Where(stats => stats.IsBackup is not true)
                .ToList();

            if (active.Count == 0)
            {
                return TrackerHealth.Unsupported;
            }

            if (active.Any(stats => stats.LastAnnounceSucceeded is true))
            {
                return TrackerHealth.Working;
            }

            // Announce state 3 is TR_TRACKER_ACTIVE: an announce is in flight and has no result yet.
            if (active.Any(stats => stats.AnnounceState == 3))
            {
                return TrackerHealth.Inconclusive;
            }

            if (active.Any(stats => stats.HasAnnounced is not true))
            {
                return TrackerHealth.Inconclusive;
            }

            List<TransmissionTorrentTrackerStats> failing = active
                .Where(stats => stats.HasAnnounced is true && stats.LastAnnounceSucceeded is false)
                .ToList();

            if (failing.Count == 0)
            {
                return TrackerHealth.Unsupported;
            }

            if (failing.Any(stats => TrackerMessageClassifier.Classify(stats.LastAnnounceResult) is TrackerHealth.Unregistered))
            {
                return TrackerHealth.Unregistered;
            }

            return TrackerHealth.Inconclusive;
        }
    }

    /// <inheritdoc/>
    public DateTimeOffset? AddedOn => Info.AddedDate is { } addedDate
        ? DateTimeOffset.FromUnixTimeSeconds(addedDate)
        : null;

    public long Eta => Info.Eta ?? 0;
    
    public long SeedingTimeSeconds => Info.SecondsSeeding ?? 0;

    public DateTime? LastActivityTime => null;

    public string? Category
    {
        get => Info.GetCategory();
        set => Info.AppendCategory(value);
    }
    
    public string SavePath => Info.DownloadDir ?? string.Empty;

    public IReadOnlyList<string> TrackerDomains => _trackerDomains.Value;

    public IReadOnlyList<string> Tags => Info.Labels?.ToList().AsReadOnly()
        ?? (IReadOnlyList<string>)Array.Empty<string>();

    // Transmission status: 0=stopped, 1=check pending, 2=checking, 3=download pending, 4=downloading, 5=seed pending, 6=seeding
    public bool IsStopped => Info.Status == 0;

    public bool IsDownloading() => Info.Status == 4;
    public bool IsStalled() => Info is { Status: 4, RateDownload: <= 0, Eta: <= 0 };

    public bool IsIgnored(IReadOnlyList<string> ignoredDownloads)
    {
        if (ignoredDownloads.Count == 0)
        {
            return false;
        }

        foreach (string pattern in ignoredDownloads)
        {
            if (Hash?.Equals(pattern, StringComparison.InvariantCultureIgnoreCase) is true)
            {
                return true;
            }

            if (Category?.Equals(pattern, StringComparison.InvariantCultureIgnoreCase) is true)
            {
                return true;
            }

            bool? hasIgnoredTracker = Info.Trackers?
                .Any(x => UriService.GetDomain(x.Announce)?.EndsWith(pattern, StringComparison.InvariantCultureIgnoreCase) ?? false);
            
            if (hasIgnoredTracker is true)
            {
                return true;
            }
        }

        return false;
    }
}
