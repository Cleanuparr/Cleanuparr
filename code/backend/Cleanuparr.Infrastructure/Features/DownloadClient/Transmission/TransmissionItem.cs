using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Services;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

/// <summary>
/// Wrapper for Transmission TorrentInfo that implements ITorrentItem interface
/// </summary>
public sealed class TransmissionItem : ITorrentItem
{
    private readonly TorrentInfo _torrentInfo;

    public TransmissionItem(TorrentInfo torrentInfo)
    {
        _torrentInfo = torrentInfo ?? throw new ArgumentNullException(nameof(torrentInfo));
    }

    // Basic identification
    public string Hash => _torrentInfo.HashString ?? string.Empty;
    public string Name => _torrentInfo.Name ?? string.Empty;

    // Privacy and tracking
    public bool IsPrivate => _torrentInfo.IsPrivate ?? false;
    public IReadOnlyList<string> Trackers => _torrentInfo.Trackers?
        .Where(t => !string.IsNullOrEmpty(t.Announce))
        .Select(t => ExtractHostFromUrl(t.Announce!))
        .Where(host => !string.IsNullOrEmpty(host))
        .Distinct()
        .ToList()
        .AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();

    // Size and progress
    public long Size => _torrentInfo.TotalSize ?? 0;
    public double CompletionPercentage => _torrentInfo.TotalSize > 0
        ? ((_torrentInfo.DownloadedEver ?? 0) / (double)_torrentInfo.TotalSize) * 100.0
        : 0.0;
    public long DownloadedBytes => _torrentInfo.DownloadedEver ?? 0;
    public long TotalUploaded => _torrentInfo.UploadedEver ?? 0;

    // Speed and transfer rates
    public long DownloadSpeed => _torrentInfo.RateDownload ?? 0;
    public long UploadSpeed => _torrentInfo.RateUpload ?? 0;
    public double Ratio => (_torrentInfo.UploadedEver ?? 0) > 0 && (_torrentInfo.DownloadedEver ?? 0) > 0
        ? (_torrentInfo.UploadedEver ?? 0) / (double)(_torrentInfo.DownloadedEver ?? 1)
        : 0.0;

    // Time tracking
    public long Eta => _torrentInfo.Eta ?? 0;
    public DateTime? DateAdded => _torrentInfo.AddedDate.HasValue
        ? DateTimeOffset.FromUnixTimeSeconds(_torrentInfo.AddedDate.Value).DateTime
        : null;
    public DateTime? DateCompleted => _torrentInfo.DoneDate.HasValue && _torrentInfo.DoneDate.Value > 0
        ? DateTimeOffset.FromUnixTimeSeconds(_torrentInfo.DoneDate.Value).DateTime
        : null;
    public long SeedingTimeSeconds => _torrentInfo.SecondsSeeding ?? 0;

    // Categories and tags
    public string? Category => _torrentInfo.GetCategory();
    public IReadOnlyList<string> Tags => _torrentInfo.Labels?.ToList().AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();

    // State checking methods
    // Transmission status: 0=stopped, 1=check pending, 2=checking, 3=download pending, 4=downloading, 5=seed pending, 6=seeding
    public bool IsDownloading() => _torrentInfo.Status == 4;
    public bool IsStalled() => _torrentInfo is { Status: 4, RateDownload: <= 0, Eta: <= 0 };
    public bool IsSeeding() => _torrentInfo.Status == 6;
    public bool IsCompleted() => CompletionPercentage >= 100.0;
    public bool IsPaused() => _torrentInfo.Status == 0;
    public bool IsQueued() => _torrentInfo.Status is 1 or 3 or 5;
    public bool IsChecking() => _torrentInfo.Status == 2;
    public bool IsAllocating() => false; // Transmission doesn't have a specific allocating state
    public bool IsMetadataDownloading() => false; // Transmission doesn't have this state

    // Filtering methods
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

            bool? hasIgnoredTracker = _torrentInfo.Trackers?
                .Any(x => UriService.GetDomain(x.Announce)?.EndsWith(pattern, StringComparison.InvariantCultureIgnoreCase) ?? false);
            
            if (hasIgnoredTracker is true)
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
