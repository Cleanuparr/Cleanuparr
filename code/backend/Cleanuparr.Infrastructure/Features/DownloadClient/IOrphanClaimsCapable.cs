using Cleanuparr.Domain.Entities;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// Download clients that can report the on-disk paths their downloads claim, for orphaned-files cleanup.
/// Implemented by every current client (torrent and usenet).
/// </summary>
public interface IOrphanClaimsCapable : IDownloadService
{
    /// <summary>
    /// Fetches all downloads regardless of their state, without per-item tracker or properties calls.
    /// Used by the orphaned files cleanup to identify which paths are claimed by active downloads.
    /// </summary>
    /// <returns>A list of all downloads.</returns>
    Task<List<IDownloadItem>> GetAllDownloadsLite();

    /// <summary>
    /// Resolves the on-disk paths claimed by the given downloads.
    /// </summary>
    /// <returns>The distinct, remapped paths claimed by the downloads.</returns>
    Task<IReadOnlyList<string>> GetClaimedPathsAsync(IReadOnlyList<IDownloadItem> downloads);
}
