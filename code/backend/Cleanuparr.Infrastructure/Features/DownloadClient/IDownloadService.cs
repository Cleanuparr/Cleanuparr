using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

public interface IDownloadService : IDisposable
{
    DownloadClientConfig ClientConfig { get; }

    public Task LoginAsync();

    /// <summary>
    /// Performs a health check on the download client
    /// </summary>
    /// <returns>The health check result</returns>
    public Task<HealthCheckResult> HealthCheckAsync();

    /// <summary>
    /// Checks whether the download should be removed from the *arr queue.
    /// </summary>
    /// <param name="hash">The download hash.</param>
    /// <param name="ignoredDownloads">Downloads to ignore from processing.</param>
    public Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string hash,
        IReadOnlyList<string> ignoredDownloads);

    /// <summary>
    /// Fetches all seeding downloads.
    /// </summary>
    /// <returns>A list of downloads that are seeding.</returns>
    Task<List<ITorrentItem>?> GetSeedingDownloads();

    /// <summary>
    /// Filters downloads that should be cleaned.
    /// </summary>
    /// <param name="downloads">The downloads to filter.</param>
    /// <param name="categories">The categories by which to filter the downloads.</param>
    /// <returns>A list of downloads for the provided categories.</returns>
    List<ITorrentItem>? FilterDownloadsToBeCleanedAsync(List<ITorrentItem>? downloads, List<CleanCategory> categories);

    /// <summary>
    /// Filters downloads that should have their category changed.
    /// </summary>
    /// <param name="downloads">The downloads to filter.</param>
    /// <param name="categories">The categories by which to filter the downloads.</param>
    /// <returns>A list of downloads for the provided categories.</returns>
    List<ITorrentItem>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItem>? downloads, List<string> categories);

    /// <summary>
    /// Cleans the downloads.
    /// </summary>
    /// <param name="downloads">The downloads to clean.</param>
    /// <param name="categoriesToClean">The categories that should be cleaned.</param>
    /// <param name="excludedHashes">The hashes that should not be cleaned.</param>
    /// <param name="ignoredDownloads">The downloads to ignore from processing.</param>
    Task CleanDownloadsAsync(List<ITorrentItem>? downloads, List<CleanCategory> categoriesToClean, HashSet<string> excludedHashes, IReadOnlyList<string> ignoredDownloads);

    /// <summary>
    /// Changes the category for downloads that have no hardlinks.
    /// </summary>
    /// <param name="downloads">The downloads to change.</param>
    /// <param name="excludedHashes">The hashes that should not be cleaned.</param>
    /// <param name="ignoredDownloads">The downloads to ignore from processing.</param>
    Task ChangeCategoryForNoHardLinksAsync(List<ITorrentItem>? downloads, HashSet<string> excludedHashes, IReadOnlyList<string> ignoredDownloads);
    
    /// <summary>
    /// Deletes a download item.
    /// </summary>
    public Task DeleteDownload(string hash);

    /// <summary>
    /// Creates a category.
    /// </summary>
    /// <param name="name">The category name.</param>
    public Task CreateCategoryAsync(string name);
    
    /// <summary>
    /// Blocks unwanted files from being fully downloaded.
    /// </summary>
    /// <param name="hash">The torrent hash.</param>
    /// <param name="ignoredDownloads">Downloads to ignore from processing.</param>
    /// <returns>True if all files have been blocked; otherwise false.</returns>
    public Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, IReadOnlyList<string> ignoredDownloads);
}