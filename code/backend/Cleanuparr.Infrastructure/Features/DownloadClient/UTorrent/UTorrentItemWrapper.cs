using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using Cleanuparr.Infrastructure.Services;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// Wrapper for UTorrent UTorrentItem and UTorrentProperties that implements ITorrentItem interface
/// </summary>
public sealed class UTorrentItemWrapper : ITorrentItemWrapper
{
    private readonly Lazy<IReadOnlyList<string>> _trackerDomains;
    private readonly TimeProvider _timeProvider;

    public UTorrentItem Info { get; }

    public UTorrentProperties Properties { get; }

    public UTorrentItemWrapper(UTorrentItem torrentItem, UTorrentProperties torrentProperties, TimeProvider timeProvider)
    {
        Info = torrentItem ?? throw new ArgumentNullException(nameof(torrentItem));
        Properties = torrentProperties ?? throw new ArgumentNullException(nameof(torrentProperties));
        _timeProvider = timeProvider;
        _trackerDomains = new Lazy<IReadOnlyList<string>>(() => Properties.TrackerList
            .Select(url => UriService.GetDomain(url))
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList()
            .AsReadOnly());
    }

    public string Hash => Info.Hash;
    
    public string Name => Info.Name;

    public bool IsPrivate => Properties.IsPrivate;

    public long Size => Info.Size;
    
    public double CompletionPercentage => Info.Progress / 10.0; // Progress is in permille (1000 = 100%)
    
    public long DownloadedBytes => Info.Downloaded;

    public long DownloadSpeed => Info.DownloadSpeed;
    
    public double Ratio => Info.Ratio;

    /// <inheritdoc/>
    public int? SeederCount => Info.SeedsInSwarm;

    /// <inheritdoc/>
    /// <remarks>µTorrent exposes no per-tracker state: its only failure hint is a generic error bit that also covers disk errors.</remarks>
    public TrackerHealth TrackerHealth => TrackerHealth.Unsupported;

    /// <inheritdoc/>
    public DateTimeOffset? AddedOn => Info.DateAdded > 0
        ? DateTimeOffset.FromUnixTimeSeconds(Info.DateAdded)
        : null;

    public long Eta => Info.ETA;
    
    /// <inheritdoc/>
    /// <remarks>µTorrent reports no seeding time, so it is derived from the completion timestamp.</remarks>
    public long SeedingTimeSeconds => CalculateSeedingTime();

    public DateTime? LastActivityTime => null;

    public string? Category
    {
        get => Info.Label;
        set => Info.Label = value ?? throw new ArgumentNullException(nameof(value));
    }

    public string SavePath => Info.SavePath ?? string.Empty;

    public IReadOnlyList<string> TrackerDomains => _trackerDomains.Value;

    public IReadOnlyList<string> Tags => Array.Empty<string>();

    public bool IsStopped =>
        (Info.Status & UTorrentStatus.Started) == 0 ||
        (Info.Status & UTorrentStatus.Paused) != 0;

    public bool IsDownloading() =>
        (Info.Status & UTorrentStatus.Started) != 0 &&
        (Info.Status & UTorrentStatus.Checked) != 0 &&
        (Info.Status & UTorrentStatus.Error) == 0;

    public bool IsStalled() => IsDownloading() && Info is { DownloadSpeed: 0, ETA: 0 };

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

    /// <summary>
    /// Calculate seeding time based on the timestamp when the torrent finished downloading.
    /// µTorrent doesn't natively track seeding time, so we calculate it from completion timestamp.
    /// </summary>
    private long CalculateSeedingTime()
    {
        // If not finished yet, no seeding time
        if (Info.DateCompletedDateTime is null)
        {
            return 0;
        }

        long seedingTime = (long)(_timeProvider.GetUtcNow() - Info.DateCompletedDateTime.Value).TotalSeconds;
        return seedingTime > 0 ? seedingTime : 0;
    }
}
