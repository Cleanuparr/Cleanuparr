using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Deluge.Response;

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
    public IReadOnlyList<string> Trackers => Info.Trackers?
        .Where(t => !string.IsNullOrEmpty(t.Url))
        .Select(t => ExtractHostFromUrl(t.Url!))
        .Where(host => !string.IsNullOrEmpty(host))
        .Distinct()
        .ToList()
        .AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();

    // Size and progress
    public long Size => Info.Size;
    public double CompletionPercentage => Info.Size > 0
        ? (Info.TotalDone / (double)Info.Size) * 100.0
        : 0.0;
    public long DownloadedBytes => Info.TotalDone;
    public long TotalUploaded => (long)(Info.Ratio * Info.TotalDone);

    // Speed and transfer rates
    public long DownloadSpeed => Info.DownloadSpeed;
    public long UploadSpeed => 0; // Deluge DownloadStatus doesn't expose upload speed
    public double Ratio => Info.Ratio;

    // Time tracking
    public long Eta => (long)Info.Eta;
    public DateTime? DateAdded => null; // Deluge DownloadStatus doesn't expose date added
    public DateTime? DateCompleted => null; // Deluge DownloadStatus doesn't expose date completed
    public long SeedingTimeSeconds => Info.SeedingTime;

    // Categories and tags
    public string? Category => Info.Label;
    public IReadOnlyList<string> Tags => Array.Empty<string>(); // Deluge doesn't have tags

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

        return ignoredDownloads.Any(pattern =>
            Name.Contains(pattern, StringComparison.InvariantCultureIgnoreCase) ||
            Hash.Equals(pattern, StringComparison.InvariantCultureIgnoreCase) ||
            Trackers.Any(tracker => tracker.EndsWith(pattern, StringComparison.InvariantCultureIgnoreCase)));
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
