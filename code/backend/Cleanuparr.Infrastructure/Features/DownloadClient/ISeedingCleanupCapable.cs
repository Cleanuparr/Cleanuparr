using Cleanuparr.Domain.Entities;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// Download clients whose downloads seed, and so can be evaluated against seeding rules and moved on dead-torrent detection.
/// Implemented by the torrent clients only; usenet downloads never seed.
/// </summary>
public interface ISeedingCleanupCapable : IDownloadService
{
    /// <summary>
    /// Fetches all seeding downloads.
    /// </summary>
    /// <returns>A list of downloads that are seeding.</returns>
    Task<List<ITorrentItemWrapper>> GetSeedingDownloads();

    /// <summary>
    /// Filters downloads that should be cleaned.
    /// </summary>
    /// <param name="downloads">The downloads to filter.</param>
    /// <param name="seedingRules">The seeding rules by which to filter the downloads.</param>
    /// <returns>A list of downloads for the provided categories.</returns>
    List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<ISeedingRule> seedingRules);

    /// <summary>
    /// Cleans the downloads.
    /// </summary>
    /// <param name="downloads">The downloads to clean.</param>
    /// <param name="seedingRules">The seeding rules.</param>
    Task CleanDownloadsAsync(List<ITorrentItemWrapper>? downloads, List<ISeedingRule> seedingRules);

    /// <summary>
    /// Stops a download, leaving it in the client.
    /// </summary>
    /// <param name="item">The download item.</param>
    Task StopDownload(IDownloadItem item);
}
