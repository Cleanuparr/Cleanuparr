namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// Download clients that can report whether an *arr queue item should be removed.
/// Implemented by every current client (torrent and usenet).
/// </summary>
public interface IQueueCheckCapable : IDownloadService
{
    /// <summary>
    /// Checks whether the download should be removed from the *arr queue.
    /// </summary>
    /// <param name="downloadId">The download id.</param>
    /// <param name="ignoredDownloads">Downloads to ignore from processing.</param>
    Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string downloadId, IReadOnlyList<string> ignoredDownloads);
}
