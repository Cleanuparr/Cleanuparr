namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// Download clients that can block unwanted files from completing.
/// Implemented by every current client (torrent and usenet).
/// </summary>
public interface IFileBlockingCapable : IDownloadService
{
    /// <summary>
    /// Blocks unwanted files from being fully downloaded.
    /// </summary>
    /// <param name="downloadId">The download id.</param>
    /// <param name="ignoredDownloads">Downloads to ignore from processing.</param>
    /// <returns>True if all files have been blocked; otherwise false.</returns>
    Task<BlockFilesResult> BlockUnwantedFilesAsync(string downloadId, IReadOnlyList<string> ignoredDownloads);
}
