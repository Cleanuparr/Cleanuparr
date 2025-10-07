using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Infrastructure.Services;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;

/// <summary>
/// Extension methods for µTorrent entities and status checking
/// </summary>
public static class UTorrentExtensions
{
    /// <summary>
    /// Checks if the torrent is currently seeding
    /// </summary>
    public static bool IsSeeding(this UTorrentItem item)
    {
        return IsDownloading(item.Status) && item.DateCompleted > 0;
    }

    /// <summary>
    /// Checks if the torrent is currently downloading
    /// </summary>
    public static bool IsDownloading(this UTorrentItem item)
    {
        return IsDownloading(item.Status);
    }

    /// <summary>
    /// Checks if the status indicates downloading
    /// </summary>
    public static bool IsDownloading(int status)
    {
        return (status & UTorrentStatus.Started) != 0 && 
               (status & UTorrentStatus.Checked) != 0 &&
               (status & UTorrentStatus.Error) == 0;
    }
    
    /// <summary>
    /// Checks if a torrent should be ignored based on the ignored patterns
    /// </summary>
    public static bool ShouldIgnore(this UTorrentItem download, IReadOnlyList<string> ignoredDownloads)
    {
        foreach (string value in ignoredDownloads)
        {
            if (download.Hash.Equals(value, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            
            if (download.Label.Equals(value, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
    
    public static bool ShouldIgnore(this string tracker, IReadOnlyList<string> ignoredDownloads)
    {
        string? trackerUrl = UriService.GetDomain(tracker);

        if (trackerUrl is null)
        {
            return false;
        }
        
        foreach (string value in ignoredDownloads)
        {
            if (trackerUrl.EndsWith(value, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
} 