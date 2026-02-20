using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Infrastructure.Services;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

/// <summary>
/// Wrapper for Deluge DownloadStatus that implements ITorrentItem interface
/// </summary>
public sealed class DelugeItemWrapper : ITorrentItemWrapper
{
    public DownloadStatus Info { get; }

    public DelugeItemWrapper(DownloadStatus downloadStatus)
    {
        Info = downloadStatus ?? throw new ArgumentNullException(nameof(downloadStatus));
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

    public long Eta => (long)Info.Eta;
    
    public long SeedingTimeSeconds => Info.SeedingTime;

    public string? Category
    {
        get => Info.Label;
        set => Info.Label = value;
    }

    public string SavePath => Info.DownloadLocation ?? string.Empty;

    public bool IsDownloading() => Info.State?.Equals("Downloading", StringComparison.InvariantCultureIgnoreCase) == true;
    
    public bool IsStalled() => Info.State?.Equals("Downloading", StringComparison.InvariantCultureIgnoreCase) == true && Info is { DownloadSpeed: <= 0, Eta: <= 0 };

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
