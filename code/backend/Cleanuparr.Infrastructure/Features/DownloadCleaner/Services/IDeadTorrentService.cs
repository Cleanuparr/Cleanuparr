using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Features.DownloadClient;

namespace Cleanuparr.Infrastructure.Features.DownloadCleaner.Services;

/// <summary>
/// Moves torrents that have been dead (zero seeders) for a configured number of consecutive runs
/// to a target category/tag, so seeding rules can act on them.
/// </summary>
public interface IDeadTorrentService
{
    Task ProcessAsync(IDownloadService downloadService, List<ITorrentItemWrapper> clientDownloads);
}
