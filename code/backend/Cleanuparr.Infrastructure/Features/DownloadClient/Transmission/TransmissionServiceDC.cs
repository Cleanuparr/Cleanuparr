using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.Extensions.Logging;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

public partial class TransmissionService
{
    public override async Task<List<ITorrentItem>?> GetSeedingDownloads()
    {
        var result = await _client.TorrentGetAsync(Fields);
        return result?.Torrents
            ?.Where(x => !string.IsNullOrEmpty(x.HashString))
            .Where(x => x.Status is 5 or 6)
            .Select(x => (ITorrentItem)new TransmissionItem(x))
            .ToList();
    }

    /// <inheritdoc/>
    public override List<ITorrentItem>? FilterDownloadsToBeCleanedAsync(List<ITorrentItem>? downloads, List<CleanCategory> categories)
    {
        return downloads
            ?.Where(x => categories
                .Any(cat => cat.Name.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase))
            )
            .ToList();
    }

    public override List<ITorrentItem>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItem>? downloads, List<string> categories)
    {
        return downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task CleanDownloadsAsync(List<ITorrentItem>? downloads, List<CleanCategory> categoriesToClean,
        HashSet<string> excludedHashes, IReadOnlyList<string> ignoredDownloads)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }

        foreach (ITorrentItem download in downloads)
        {
            if (string.IsNullOrEmpty(download.Hash))
            {
                continue;
            }

            if (excludedHashes.Any(x => x.Equals(download.Hash, StringComparison.InvariantCultureIgnoreCase)))
            {
                _logger.LogDebug("skip | download is used by an arr | {name}", download.Name);
                continue;
            }

            if (download.IsIgnored(ignoredDownloads))
            {
                _logger.LogDebug("skip | download is ignored | {name}", download.Name);
                continue;
            }

            CleanCategory? category = categoriesToClean
                .FirstOrDefault(x => x.Name.Equals(download.Category, StringComparison.InvariantCultureIgnoreCase));

            if (category is null)
            {
                continue;
            }

            var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));

            if (!downloadCleanerConfig.DeletePrivate && download.IsPrivate)
            {
                _logger.LogDebug("skip | download is private | {name}", download.Name);
                continue;
            }

            ContextProvider.Set("downloadName", download.Name);
            ContextProvider.Set("hash", download.Hash);

            TimeSpan seedingTime = TimeSpan.FromSeconds(download.SeedingTimeSeconds);
            SeedingCheckResult result = ShouldCleanDownload(download.Ratio, seedingTime, category);

            if (!result.ShouldClean)
            {
                continue;
            }

            // Get the underlying TorrentInfo to access Id for deletion
            TorrentInfo? torrentInfo = await GetTorrentAsync(download.Hash);
            if (torrentInfo is null)
            {
                _logger.LogDebug("failed to find torrent info for {name}", download.Name);
                continue;
            }

            await _dryRunInterceptor.InterceptAsync(RemoveDownloadAsync, torrentInfo.Id);

            _logger.LogInformation(
                "download cleaned | {reason} reached | {name}",
                result.Reason is CleanReason.MaxRatioReached
                    ? "MAX_RATIO & MIN_SEED_TIME"
                    : "MAX_SEED_TIME",
                download.Name
            );

            await _eventPublisher.PublishDownloadCleaned(download.Ratio, seedingTime, category.Name, result.Reason);
        }
    }
    
    public override async Task CreateCategoryAsync(string name)
    {
        await Task.CompletedTask;
    }

    public override async Task ChangeCategoryForNoHardLinksAsync(List<ITorrentItem>? downloads, HashSet<string> excludedHashes, IReadOnlyList<string> ignoredDownloads)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }

        var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));

        if (!string.IsNullOrEmpty(downloadCleanerConfig.UnlinkedIgnoredRootDir))
        {
            _hardLinkFileService.PopulateFileCounts(downloadCleanerConfig.UnlinkedIgnoredRootDir);
        }

        foreach (ITorrentItem download in downloads)
        {
            if (string.IsNullOrEmpty(download.Hash) || string.IsNullOrEmpty(download.Name))
            {
                continue;
            }

            if (excludedHashes.Any(x => x.Equals(download.Hash, StringComparison.InvariantCultureIgnoreCase)))
            {
                _logger.LogDebug("skip | download is used by an arr | {name}", download.Name);
                continue;
            }

            if (download.IsIgnored(ignoredDownloads))
            {
                _logger.LogDebug("skip | download is ignored | {name}", download.Name);
                continue;
            }

            ContextProvider.Set("downloadName", download.Name);
            ContextProvider.Set("hash", download.Hash);

            // Get the underlying TorrentInfo to access files and DownloadDir
            TorrentInfo? torrentInfo = await GetTorrentAsync(download.Hash);
            if (torrentInfo is null || torrentInfo.DownloadDir is null)
            {
                _logger.LogDebug("failed to find torrent info for {name}", download.Name);
                continue;
            }

            bool hasHardlinks = false;

            if (torrentInfo.Files is null || torrentInfo.FileStats is null)
            {
                _logger.LogDebug("skip | download has no files | {name}", download.Name);
                continue;
            }

            for (int i = 0; i < torrentInfo.Files.Length; i++)
            {
                TransmissionTorrentFiles file = torrentInfo.Files[i];
                TransmissionTorrentFileStats stats = torrentInfo.FileStats[i];

                if (stats.Wanted is null or false || string.IsNullOrEmpty(file.Name))
                {
                    continue;
                }

                string filePath = string.Join(Path.DirectorySeparatorChar, Path.Combine(torrentInfo.DownloadDir, file.Name).Split(['\\', '/']));

                long hardlinkCount = _hardLinkFileService.GetHardLinkCount(filePath, !string.IsNullOrEmpty(downloadCleanerConfig.UnlinkedIgnoredRootDir));

                if (hardlinkCount < 0)
                {
                    _logger.LogDebug("skip | could not get file properties | {file}", filePath);
                    hasHardlinks = true;
                    break;
                }

                if (hardlinkCount > 0)
                {
                    hasHardlinks = true;
                    break;
                }
            }

            if (hasHardlinks)
            {
                _logger.LogDebug("skip | download has hardlinks | {name}", download.Name);
                continue;
            }

            string currentCategory = download.Category ?? string.Empty;
            string newLocation = string.Join(Path.DirectorySeparatorChar, Path.Combine(torrentInfo.DownloadDir, downloadCleanerConfig.UnlinkedTargetCategory).Split(['\\', '/']));

            await _dryRunInterceptor.InterceptAsync(ChangeDownloadLocation, torrentInfo.Id, newLocation);

            _logger.LogInformation("category changed for {name}", download.Name);

            await _eventPublisher.PublishCategoryChanged(currentCategory, downloadCleanerConfig.UnlinkedTargetCategory);
        }
    }

    protected virtual async Task ChangeDownloadLocation(long downloadId, string newLocation)
    {
        await _client.TorrentSetLocationAsync([downloadId], newLocation, true);
    }

    public override async Task DeleteDownload(string hash)
    {
        TorrentInfo? torrent = await GetTorrentAsync(hash);

        if (torrent is null)
        {
            return;
        }

        await _client.TorrentRemoveAsync([torrent.Id], true);
    }
    
    protected virtual async Task RemoveDownloadAsync(long downloadId)
    {
        await _client.TorrentRemoveAsync([downloadId], true);
    }
}