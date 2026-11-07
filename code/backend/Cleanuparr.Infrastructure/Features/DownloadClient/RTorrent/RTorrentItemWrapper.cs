using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.RTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;

/// <summary>
/// Wrapper for RTorrentTorrent that implements ITorrentItemWrapper interface
/// </summary>
public sealed class RTorrentItemWrapper : ITorrentItemWrapper
{
    public RTorrentTorrent Info { get; }
    private readonly IReadOnlyList<string> _trackers;
    private readonly Lazy<IReadOnlyList<string>> _trackerDomains;
    private string? _category;
    private readonly TimeProvider _timeProvider;

    public RTorrentItemWrapper(RTorrentTorrent torrent, IReadOnlyList<string>? trackers, TimeProvider timeProvider)
    {
        Info = torrent ?? throw new ArgumentNullException(nameof(torrent));
        _timeProvider = timeProvider;
        _trackers = trackers ?? torrent.Trackers ?? [];
        _category = torrent.Label;
        _trackerDomains = new Lazy<IReadOnlyList<string>>(() => _trackers
            .Select(url => UriService.GetDomain(url))
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList()
            .AsReadOnly());
    }

    public string DownloadId => Info.Hash;

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

    /// <inheritdoc/>
    /// <remarks>rTorrent does not expose seeder counts; always returns <see langword="null"/>.</remarks>
    public int? SeederCount => null;

    /// <inheritdoc/>
    /// <remarks>Cleanuparr fetches no rTorrent tracker state, and the API rejects this feature for rTorrent; always returns <see cref="TrackerHealth.Unsupported"/>.</remarks>
    public TrackerHealth TrackerHealth => TrackerHealth.Unsupported;

    /// <inheritdoc/>
    /// <remarks>rTorrent does not expose the time a torrent was added; always returns <see langword="null"/>.</remarks>
    public DateTimeOffset? AddedOn => null;

    public long Eta => CalculateEta();

    public long SeedingTimeSeconds => CalculateSeedingTime();

    public DateTime? LastActivityTime => null;

    public string? Category
    {
        get => _category;
        set => _category = value;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// rTorrent empties d.base_path on closed torrents after a restart; d.directory survives.
    /// For a single-file torrent, d.directory holds the parent folder.
    /// </remarks>
    public string SavePath
    {
        get
        {
            if (!string.IsNullOrEmpty(Info.BasePath))
            {
                return Info.BasePath;
            }

            if (string.IsNullOrEmpty(Info.Directory))
            {
                return string.Empty;
            }

            return Info.IsMultiFile == 1 ? Info.Directory : Path.Combine(Info.Directory, Info.Name);
        }
    }

    public IReadOnlyList<string> TrackerDomains => _trackerDomains.Value;

    public IReadOnlyList<string> Tags => Array.Empty<string>();

    /// <summary>
    /// Stopped when state is 0, or paused (state 1, not active)
    /// </summary>
    public bool IsStopped => Info.State == 0 || Info.IsActive == 0;

    /// <summary>
    /// Downloading when started, active and not finished
    /// </summary>
    public bool IsDownloading() => Info is { State: 1, IsActive: 1, Complete: 0 };

    /// <summary>
    /// rTorrent reports no error state.
    /// </summary>
    public bool IsErrored() => false;

    /// <inheritdoc/>
    public string? ErrorMessage => null;

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
            if (DownloadId.Equals(pattern, StringComparison.InvariantCultureIgnoreCase))
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

        long now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var seedingTime = now - Info.TimestampFinished;
        return seedingTime > 0 ? seedingTime : 0;
    }
}
