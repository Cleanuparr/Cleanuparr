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

    public string Hash => Info.HashString ?? string.Empty;
    
    public string Name => Info.Name ?? string.Empty;

    public bool IsPrivate => Info.IsPrivate ?? false;

    public long Size => Info.TotalSize ?? 0;
    
    public double CompletionPercentage => Info.TotalSize > 0
        ? ((Info.DownloadedEver ?? 0) / (double)Info.TotalSize) * 100.0
        : 0.0;
    
    public long DownloadedBytes => Info.DownloadedEver ?? 0;

    public long DownloadSpeed => Info.RateDownload ?? 0;
    
    public double Ratio => (Info.UploadedEver ?? 0) > 0 && (Info.DownloadedEver ?? 0) > 0
        ? (Info.UploadedEver ?? 0) / (double)(Info.DownloadedEver ?? 1)
        : 0.0;

    public long Eta => Info.Eta ?? 0;
    
    public long SeedingTimeSeconds => Info.SecondsSeeding ?? 0;

    public string? Category => Info.Labels?.FirstOrDefault();

    // Transmission status: 0=stopped, 1=check pending, 2=checking, 3=download pending, 4=downloading, 5=seed pending, 6=seeding
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
