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

    // Basic identification
    public string Hash => Info.Hash ?? string.Empty;
    public string Name => Info.Name ?? string.Empty;

    // Privacy and tracking
    public bool IsPrivate => Info.Private;

    // Size and progress
    public long Size => Info.Size;
    public double CompletionPercentage => Info.Size > 0
        ? (Info.TotalDone / (double)Info.Size) * 100.0
        : 0.0;
    public long DownloadedBytes => Info.TotalDone;

    // Speed and transfer rates
    public long DownloadSpeed => Info.DownloadSpeed;
    public double Ratio => Info.Ratio;

    // Time tracking
    public long Eta => (long)Info.Eta;
    public DateTime? DateAdded => null; // Deluge DownloadStatus doesn't expose date added
    public DateTime? DateCompleted => null; // Deluge DownloadStatus doesn't expose date completed
    public long SeedingTimeSeconds => Info.SeedingTime;

    // Categories and tags
    public string? Category => Info.Label;

    // State checking methods
    public bool IsDownloading() => Info.State?.Equals("Downloading", StringComparison.InvariantCultureIgnoreCase) == true;
    public bool IsStalled() => Info.State?.Equals("Downloading", StringComparison.InvariantCultureIgnoreCase) == true && Info.DownloadSpeed == 0 && Info.Eta == 0;
    public bool IsSeeding() => Info.State?.Equals("Seeding", StringComparison.InvariantCultureIgnoreCase) == true;
    public bool IsCompleted() => CompletionPercentage >= 100.0;
    public bool IsPaused() => Info.State?.Equals("Paused", StringComparison.InvariantCultureIgnoreCase) == true;
    public bool IsQueued() => Info.State?.Equals("Queued", StringComparison.InvariantCultureIgnoreCase) == true;
    public bool IsChecking() => Info.State?.Equals("Checking", StringComparison.InvariantCultureIgnoreCase) == true;
    public bool IsAllocating() => Info.State?.Equals("Allocating", StringComparison.InvariantCultureIgnoreCase) == true;

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
            
            if (Info.Trackers.Any(x => UriService.GetDomain(x.Url)?.EndsWith(pattern, StringComparison.InvariantCultureIgnoreCase) is true))
            {
                return true;
            }
        }

        return false;
    }
}
