using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Features.DownloadClient;

namespace Cleanuparr.Infrastructure.Features.DownloadCleaner.Services;

/// <summary>
/// Moves torrents that have been dead (zero seeders) for a configured number of consecutive runs
/// to a target category/tag, so seeding rules can act on them.
/// </summary>
public interface IDeadTorrentService
{
    /// <summary>
    /// Strikes torrents reporting zero seeders and moves those that reach the configured limit
    /// to the target category/tag for the given client run.
    /// </summary>
    /// <param name="downloadService">Download-client service for the current client.</param>
    /// <param name="clientDownloads">The client's torrent items to evaluate.</param>
    Task ProcessAsync(IDownloadService downloadService, List<ITorrentItemWrapper> clientDownloads);
}
