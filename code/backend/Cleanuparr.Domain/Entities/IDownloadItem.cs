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

    long DownloadSpeed { get; }

    string? Category { get; set; }

    string SavePath { get; }

    /// <summary>
    /// Whether the download client has this item stopped or paused.
    /// </summary>
    bool IsStopped { get; }

    bool IsDownloading();

    /// <summary>
    /// Determines if this item should be ignored based on the provided patterns.
    /// Checks if any pattern matches the item name, download id, or tracker.
    /// </summary>
    /// <param name="ignoredDownloads">List of patterns to check against</param>
    /// <returns>True if the item matches any ignore pattern</returns>
    bool IsIgnored(IReadOnlyList<string> ignoredDownloads);
}
