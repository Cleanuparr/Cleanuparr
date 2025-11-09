using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Infrastructure.Services;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

/// <summary>
/// Wrapper for Deluge DownloadStatus that implements ITorrentItem interface
/// </summary>
public sealed class DelugeItem : ITorrentItem
{
    private readonly DownloadStatus _downloadStatus;

    public DelugeItem(DownloadStatus downloadStatus)
    {
        _downloadStatus = downloadStatus ?? throw new ArgumentNullException(nameof(downloadStatus));
    }

    // Basic identification
    public string Hash => _downloadStatus.Hash ?? string.Empty;
    public string Name => _downloadStatus.Name ?? string.Empty;

    // Privacy and tracking
    public bool IsPrivate => _downloadStatus.Private;
    public IReadOnlyList<string> Trackers => _downloadStatus.Trackers?
        .Where(t => !string.IsNullOrEmpty(t.Url))
        .Select(t => ExtractHostFromUrl(t.Url!))
        .Where(host => !string.IsNullOrEmpty(host))
        .Distinct()
        .ToList()
        .AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();

    // Size and progress
    public long Size => _downloadStatus.Size;
    public double CompletionPercentage => _downloadStatus.Size > 0
        ? (_downloadStatus.TotalDone / (double)_downloadStatus.Size) * 100.0
        : 0.0;
    public long DownloadedBytes => _downloadStatus.TotalDone;
    public long TotalUploaded => (long)(_downloadStatus.Ratio * _downloadStatus.TotalDone);

    // Speed and transfer rates
    public long DownloadSpeed => _downloadStatus.DownloadSpeed;
    public long UploadSpeed => 0; // Deluge DownloadStatus doesn't expose upload speed
    public double Ratio => _downloadStatus.Ratio;

    // Time tracking
    public long Eta => (long)_downloadStatus.Eta;
    public DateTime? DateAdded => null; // Deluge DownloadStatus doesn't expose date added
    public DateTime? DateCompleted => null; // Deluge DownloadStatus doesn't expose date completed
    public long SeedingTimeSeconds => _downloadStatus.SeedingTime;

    // Categories and tags
    public string? Category => _downloadStatus.Label;
    public IReadOnlyList<string> Tags => Array.Empty<string>(); // Deluge doesn't have tags

    // State checking methods
    public bool IsDownloading() => _downloadStatus.State?.Equals("Downloading", StringComparison.InvariantCultureIgnoreCase) == true;
    public bool IsStalled() => _downloadStatus.State?.Equals("Downloading", StringComparison.InvariantCultureIgnoreCase) == true && _downloadStatus.DownloadSpeed == 0 && _downloadStatus.Eta == 0;
    public bool IsSeeding() => _downloadStatus.State?.Equals("Seeding", StringComparison.InvariantCultureIgnoreCase) == true;
    public bool IsCompleted() => CompletionPercentage >= 100.0;
    public bool IsPaused() => _downloadStatus.State?.Equals("Paused", StringComparison.InvariantCultureIgnoreCase) == true;
    public bool IsQueued() => _downloadStatus.State?.Equals("Queued", StringComparison.InvariantCultureIgnoreCase) == true;
    public bool IsChecking() => _downloadStatus.State?.Equals("Checking", StringComparison.InvariantCultureIgnoreCase) == true;
    public bool IsAllocating() => _downloadStatus.State?.Equals("Allocating", StringComparison.InvariantCultureIgnoreCase) == true;
    public bool IsMetadataDownloading() => false; // Deluge doesn't have this state

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
            
            if (_downloadStatus.Trackers.Any(x => UriService.GetDomain(x.Url)?.EndsWith(pattern, StringComparison.InvariantCultureIgnoreCase) is true))
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
