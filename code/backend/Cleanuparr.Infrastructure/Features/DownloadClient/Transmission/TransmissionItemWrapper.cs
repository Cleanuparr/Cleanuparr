using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Services;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

/// <summary>
/// Wrapper for Transmission TorrentInfo that implements ITorrentItem interface
/// </summary>
public sealed class TransmissionItemWrapper : ITorrentItemWrapper
{
    public TorrentInfo Info { get; }

    public TransmissionItemWrapper(TorrentInfo torrentInfo)
    {
        Info = torrentInfo ?? throw new ArgumentNullException(nameof(torrentInfo));
    }

    // Basic identification
    public string Hash => Info.HashString ?? string.Empty;
    public string Name => Info.Name ?? string.Empty;

    // Privacy and tracking
    public bool IsPrivate => Info.IsPrivate ?? false;

    // Size and progress
    public long Size => Info.TotalSize ?? 0;
    public double CompletionPercentage => Info.TotalSize > 0
        ? ((Info.DownloadedEver ?? 0) / (double)Info.TotalSize) * 100.0
        : 0.0;
    public long DownloadedBytes => Info.DownloadedEver ?? 0;
    public long TotalUploaded => Info.UploadedEver ?? 0;

    // Speed and transfer rates
    public long DownloadSpeed => Info.RateDownload ?? 0;
    public long UploadSpeed => Info.RateUpload ?? 0;
    public double Ratio => (Info.UploadedEver ?? 0) > 0 && (Info.DownloadedEver ?? 0) > 0
        ? (Info.UploadedEver ?? 0) / (double)(Info.DownloadedEver ?? 1)
        : 0.0;

    // Time tracking
    public long Eta => Info.Eta ?? 0;
    public DateTime? DateAdded => Info.AddedDate.HasValue
        ? DateTimeOffset.FromUnixTimeSeconds(Info.AddedDate.Value).DateTime
        : null;
    public DateTime? DateCompleted => Info.DoneDate.HasValue && Info.DoneDate.Value > 0
        ? DateTimeOffset.FromUnixTimeSeconds(Info.DoneDate.Value).DateTime
        : null;
    public long SeedingTimeSeconds => Info.SecondsSeeding ?? 0;

    // Categories and tags
    public string? Category => Info.Labels?.FirstOrDefault();
    public IReadOnlyList<string> Tags => Info.Labels?.ToList().AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();

    // State checking methods
    // Transmission status: 0=stopped, 1=check pending, 2=checking, 3=download pending, 4=downloading, 5=seed pending, 6=seeding
    public bool IsDownloading() => Info.Status == 4;
    public bool IsStalled() => Info is { Status: 4, RateDownload: <= 0, Eta: <= 0 };
    public bool IsSeeding() => Info.Status == 6;
    public bool IsCompleted() => CompletionPercentage >= 100.0;
    public bool IsPaused() => Info.Status == 0;
    public bool IsQueued() => Info.Status is 1 or 3 or 5;
    public bool IsChecking() => Info.Status == 2;

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
