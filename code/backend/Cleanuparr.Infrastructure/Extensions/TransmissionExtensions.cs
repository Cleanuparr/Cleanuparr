using Cleanuparr.Infrastructure.Services;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Extensions;

public static class TransmissionExtensions
{
    public static bool ShouldIgnore(this TorrentInfo download, IReadOnlyList<string> ignoredDownloads)
    {
        foreach (string value in ignoredDownloads)
        {
            if (download.HashString?.Equals(value, StringComparison.InvariantCultureIgnoreCase) ?? false)
            {
                return true;
            }

            if (download.GetCategory().Equals(value, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            bool? hasIgnoredTracker = download.Trackers?
                .Any(x => UriService.GetDomain(x.Announce)?.EndsWith(value, StringComparison.InvariantCultureIgnoreCase) ?? false);
            
            if (hasIgnoredTracker is true)
            {
                return true;
            }
        }

        return false;
    }

    public static string GetCategory(this TorrentInfo torrent)
    {
        if (string.IsNullOrEmpty(torrent.DownloadDir))
        {
            return string.Empty;
        }

        return Path.GetFileName(Path.TrimEndingDirectorySeparator(torrent.DownloadDir));
    }
    
    /// <summary>
    /// Appends a category to the download directory of the torrent.
    /// </summary>
    public static void AppendCategory(this TorrentInfo torrent, string category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return;
        }

        torrent.DownloadDir = torrent.GetNewLocationByAppend(category);
    }
    
    public static string GetNewLocationByAppend(this TorrentInfo torrent, string category)
    {
        if (string.IsNullOrEmpty(category))
        {
            throw new ArgumentException("Category cannot be null or empty", nameof(category));
        }
        
        if (string.IsNullOrEmpty(torrent.DownloadDir))
        {
            throw new ArgumentException("DownloadDir cannot be null or empty", nameof(torrent.DownloadDir));
        }

        return string.Join(Path.DirectorySeparatorChar, Path.Combine(torrent.DownloadDir, category).Split(['\\', '/']));
    }
}