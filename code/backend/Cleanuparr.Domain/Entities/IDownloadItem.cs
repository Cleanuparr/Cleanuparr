namespace Cleanuparr.Domain.Entities;

/// <summary>
/// Universal abstraction for a download item across all download clients, torrent or usenet.
/// </summary>
public interface IDownloadItem
{
    string DownloadId { get; }

    string Name { get; }

    long Size { get; }

    double CompletionPercentage { get; }

    long DownloadedBytes { get; }

    string? Category { get; set; }

    string SavePath { get; }

    /// <summary>
    /// Determines if this item should be ignored based on the provided patterns.
    /// Checks if any pattern matches the download id, category, or a client-specific field such as a torrent tag or tracker.
    /// </summary>
    /// <param name="ignoredDownloads">List of patterns to check against</param>
    /// <returns>True if the item matches any ignore pattern</returns>
    bool IsIgnored(IReadOnlyList<string> ignoredDownloads);
}
