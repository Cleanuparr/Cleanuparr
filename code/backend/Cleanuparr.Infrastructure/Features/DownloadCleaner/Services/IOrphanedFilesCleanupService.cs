using Cleanuparr.Infrastructure.Features.DownloadClient;

namespace Cleanuparr.Infrastructure.Features.DownloadCleaner.Services;

/// <summary>
/// Scans configured directories for files that aren't claimed by any active
/// torrent and moves them to a dedicated orphaned directory. Optionally
/// purges old entries from the orphaned directory.
/// </summary>
public interface IOrphanedFilesCleanupService
{
    /// <summary>
    /// Processes orphaned files for every enabled per-client configuration.
    /// Claims are computed across all download clients to stay safe with
    /// cross-seeded torrents.
    /// </summary>
    Task ProcessAsync(IReadOnlyList<IDownloadService> downloadServices, CancellationToken cancellationToken);
}
