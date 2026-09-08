using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

/// <summary>
/// Wrapper for Deluge DownloadStatus that implements ITorrentItem interface
/// </summary>
public sealed class DelugeItemWrapper : ITorrentItemWrapper
{
    private readonly Lazy<IReadOnlyList<string>> _trackerDomains;

    public DownloadStatus Info { get; }

    public DelugeItemWrapper(DownloadStatus downloadStatus)
    {
        Info = downloadStatus ?? throw new ArgumentNullException(nameof(downloadStatus));
        _trackerDomains = new Lazy<IReadOnlyList<string>>(() => Info.Trackers
            .Select(t => UriService.GetDomain(t.Url))
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList()
            .AsReadOnly());
    }

    public string Hash => Info.Hash ?? string.Empty;
    
    public string Name => Info.Name ?? string.Empty;

    public bool IsPrivate => Info.Private;

    public long Size => Info.Size;
    
    public double CompletionPercentage => Info.Size > 0
        ? (Info.TotalDone / (double)Info.Size) * 100.0
        : 0.0;
    
    public long DownloadedBytes => Info.TotalDone;

    public long DownloadSpeed => Info.DownloadSpeed;
    
    public double Ratio => Info.Ratio;

    /// <inheritdoc/>
    public int? SeederCount => Info.TotalSeeds;

    /// <inheritdoc/>
    /// <remarks>Deluge exposes no per-tracker announce result: <c>tracker_status</c> is one free-form string for the whole torrent, and the status key on <c>trackers</c> is a snapshot frozen when the torrent was added.</remarks>
    public TrackerHealth TrackerHealth => TrackerHealth.Unsupported;

    /// <inheritdoc/>
    /// <remarks>The requested Deluge status fields carry no added time; always returns <see langword="null"/>.</remarks>
    public DateTimeOffset? AddedOn => null;

    /// <summary>
    /// The number of seconds that the download needs to finish.
    /// A negative value from Deluge shows an unknown time.
    /// This property gives 0 for a negative value.
    /// </summary>
    public long Eta => Info.Eta < 0 ? 0 : Info.Eta;
    
    public long SeedingTimeSeconds => Info.SeedingTime;

    public DateTime? LastActivityTime => null;

    public string? Category
    {
        get => Info.Label;
        set => Info.Label = value;
    }

    public string SavePath => Info.DownloadLocation ?? string.Empty;

    public IReadOnlyList<string> TrackerDomains => _trackerDomains.Value;

    public IReadOnlyList<string> Tags => Array.Empty<string>();

    public bool IsStopped => Info.State is DelugeState.Paused;

    public bool IsDownloading() => Info is { State: DelugeState.Downloading };

    /// <summary>
    /// True if the download makes no progress.
    /// </summary>
    /// <remarks>
    /// A negative eta shows an unknown time, and it is not a stalled download.
    /// </remarks>
    public bool IsStalled() => Info is { State: DelugeState.Downloading, DownloadSpeed: <= 0, Eta: 0 };

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
            
            if (Info.Trackers.Any(x => UriService.GetDomain(x.Url)?.EndsWith(pattern, StringComparison.InvariantCultureIgnoreCase) is true))
            {
                return true;
            }
        }

        return false;
    }
}
