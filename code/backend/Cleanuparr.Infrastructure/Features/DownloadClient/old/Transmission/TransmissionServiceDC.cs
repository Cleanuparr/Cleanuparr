using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.Extensions.Logging;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

public partial class TransmissionService
{
    public override async Task<List<object>?> GetSeedingDownloads() =>
        (await _client.TorrentGetAsync(Fields))
        ?.Torrents
        ?.Where(x => !string.IsNullOrEmpty(x.HashString))
        .Where(x => x.Status is 5 or 6)
        .Cast<object>()
        .ToList();

    /// <inheritdoc/>
    public override List<object>? FilterDownloadsToBeCleanedAsync(List<object>? downloads, List<CleanCategory> categories)
    {
        return downloads
            ?
            .Cast<TorrentInfo>()
            .Where(x => categories
                .Any(cat => cat.Name.Equals(x.GetCategory(), StringComparison.InvariantCultureIgnoreCase))
            )
            .Cast<object>()
            .ToList();
    }

    public override List<object>? FilterDownloadsToChangeCategoryAsync(List<object>? downloads, List<string> categories)
    {
        return downloads
            ?.Cast<TorrentInfo>()
            .Where(x => !string.IsNullOrEmpty(x.HashString))
            .Where(x => categories.Any(cat => cat.Equals(x.GetCategory(), StringComparison.InvariantCultureIgnoreCase)))
            .Cast<object>()
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task CleanDownloadsAsync(List<object>? downloads, List<CleanCategory> categoriesToClean,
        HashSet<string> excludedHashes, IReadOnlyList<string> ignoredDownloads)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }
        
        foreach (TorrentInfo download in downloads)
        {
            if (string.IsNullOrEmpty(download.HashString))
            {
                continue;
            }
            
            if (excludedHashes.Any(x => x.Equals(download.HashString, StringComparison.InvariantCultureIgnoreCase)))
            {
                _logger.LogDebug("skip | download is used by an arr | {name}", download.Name);
                continue;
            }

            if (ignoredDownloads.Count > 0 && download.ShouldIgnore(ignoredDownloads))
            {
                _logger.LogDebug("skip | download is ignored | {name}", download.Name);
                continue;
            }
            
            CleanCategory? category = categoriesToClean
                .FirstOrDefault(x =>
                {
                    if (download.DownloadDir is null)
                    {
                        return false;
                    }

                    return Path.GetFileName(Path.TrimEndingDirectorySeparator(download.DownloadDir))
                        .Equals(x.Name, StringComparison.InvariantCultureIgnoreCase);
                });
            
            if (category is null)
            {
                continue;
            }
            
            var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));

            if (!downloadCleanerConfig.DeletePrivate && download.IsPrivate is true)
            {
                _logger.LogDebug("skip | download is private | {name}", download.Name);
                continue;
            }
            
            ContextProvider.Set("downloadName", download.Name);
            ContextProvider.Set("hash", download.HashString);

            TimeSpan seedingTime = TimeSpan.FromSeconds(download.SecondsSeeding ?? 0);
            SeedingCheckResult result = ShouldCleanDownload(download.uploadRatio ?? 0, seedingTime, category);
            
            if (!result.ShouldClean)
            {
                continue;
            }

            await _dryRunInterceptor.InterceptAsync(RemoveDownloadAsync, download.Id);

            _logger.LogInformation(
                "download cleaned | {reason} reached | {name}",
                result.Reason is CleanReason.MaxRatioReached
                    ? "MAX_RATIO & MIN_SEED_TIME"
                    : "MAX_SEED_TIME",
                download.Name
            );

            await _eventPublisher.PublishDownloadCleaned(download.uploadRatio ?? 0, seedingTime, category.Name, result.Reason);
        }
    }
    
    public override async Task CreateCategoryAsync(string name)
    {
        await Task.CompletedTask;
    }

    public override async Task ChangeCategoryForNoHardLinksAsync(List<object>? downloads, HashSet<string> excludedHashes, IReadOnlyList<string> ignoredDownloads)
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
        
        foreach (TorrentInfo download in downloads.Cast<TorrentInfo>())
        {
            if (string.IsNullOrEmpty(download.HashString) || string.IsNullOrEmpty(download.Name) || download.DownloadDir == null)
            {
                continue;
            }
            
            if (excludedHashes.Any(x => x.Equals(download.HashString, StringComparison.InvariantCultureIgnoreCase)))
            {
                _logger.LogDebug("skip | download is used by an arr | {name}", download.Name);
                continue;
            }
            
            if (ignoredDownloads.Count > 0 && download.ShouldIgnore(ignoredDownloads))
            {
                _logger.LogDebug("skip | download is ignored | {name}", download.Name);
                continue;
            }
        
            ContextProvider.Set("downloadName", download.Name);
            ContextProvider.Set("hash", download.HashString);
            
            bool hasHardlinks = false;
            
            if (download.Files is null || download.FileStats is null)
            {
                _logger.LogDebug("skip | download has no files | {name}", download.Name);
                continue;
            }

            for (int i = 0; i < download.Files.Length; i++)
            {
                TransmissionTorrentFiles file = download.Files[i];
                TransmissionTorrentFileStats stats = download.FileStats[i];
                
                if (stats.Wanted is null or false || string.IsNullOrEmpty(file.Name))
                {
                    continue;
                }

                string filePath = string.Join(Path.DirectorySeparatorChar, Path.Combine(download.DownloadDir, file.Name).Split(['\\', '/']));
                
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
            
            string currentCategory = download.GetCategory();
            string newLocation = string.Join(Path.DirectorySeparatorChar, Path.Combine(download.DownloadDir, downloadCleanerConfig.UnlinkedTargetCategory).Split(['\\', '/']));
            
            await _dryRunInterceptor.InterceptAsync(ChangeDownloadLocation, download.Id, newLocation);
            
            _logger.LogInformation("category changed for {name}", download.Name);
            
            await _eventPublisher.PublishCategoryChanged(currentCategory, downloadCleanerConfig.UnlinkedTargetCategory);

            download.DownloadDir = newLocation;
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