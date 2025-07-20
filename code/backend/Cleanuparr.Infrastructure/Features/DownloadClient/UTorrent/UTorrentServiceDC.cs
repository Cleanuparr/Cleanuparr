using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

public partial class UTorrentService
{
    public override async Task<List<object>?> GetSeedingDownloads()
    {
        var torrents = await _client.GetTorrentsAsync();
        return torrents
            .Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => x.IsSeeding())
            .Cast<object>()
            .ToList();
    }

    public override List<object>? FilterDownloadsToBeCleanedAsync(List<object>? downloads, List<CleanCategory> categories) =>
        downloads
            ?.Cast<UTorrentItem>()
            .Where(x => categories.Any(cat => cat.Name.Equals(x.Label, StringComparison.InvariantCultureIgnoreCase)))
            .Cast<object>()
            .ToList();

    public override List<object>? FilterDownloadsToChangeCategoryAsync(List<object>? downloads, List<string> categories) =>
        downloads
            ?.Cast<UTorrentItem>()
            .Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => categories.Any(cat => cat.Equals(x.Label, StringComparison.InvariantCultureIgnoreCase)))
            .Cast<object>()
            .ToList();

    /// <inheritdoc/>
    public override async Task CleanDownloadsAsync(List<object>? downloads, List<CleanCategory> categoriesToClean, HashSet<string> excludedHashes,
        IReadOnlyList<string> ignoredDownloads)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }
        
        foreach (UTorrentItem download in downloads.Cast<UTorrentItem>())
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

            var properties = await _client.GetTorrentPropertiesAsync(download.Hash);
        
            if (ignoredDownloads.Count > 0 &&
                (download.ShouldIgnore(ignoredDownloads) || properties.TrackerList.Any(x => x.ShouldIgnore(ignoredDownloads))))
            {
                _logger.LogInformation("skip | download is ignored | {name}", download.Name);
                continue;
            }
            
            CleanCategory? category = categoriesToClean
                .FirstOrDefault(x => x.Name.Equals(download.Label, StringComparison.InvariantCultureIgnoreCase));

            if (category is null)
            {
                continue;
            }
            
            var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));
            
            if (!downloadCleanerConfig.DeletePrivate && properties.IsPrivate)
            {
                _logger.LogDebug("skip | download is private | {name}", download.Name);
                continue;
            }
            
            ContextProvider.Set("downloadName", download.Name);
            ContextProvider.Set("hash", download.Hash);
            
            TimeSpan? seedingTime = download.SeedingTime;
            if (seedingTime == null)
            {
                _logger.LogDebug("skip | could not determine seeding time | {name}", download.Name);
                continue;
            }
            
            SeedingCheckResult result = ShouldCleanDownload(download.Ratio, seedingTime.Value, category);

            if (!result.ShouldClean)
            {
                continue;
            }

            await _dryRunInterceptor.InterceptAsync(DeleteDownload, download.Hash);

            _logger.LogInformation(
                "download cleaned | {reason} reached | {name}",
                result.Reason is CleanReason.MaxRatioReached
                    ? "MAX_RATIO & MIN_SEED_TIME"
                    : "MAX_SEED_TIME",
                download.Name
            );
            
            await _eventPublisher.PublishDownloadCleaned(download.Ratio, seedingTime.Value, category.Name, result.Reason);
        }
    }

    public override async Task CreateCategoryAsync(string name)
    {
        var existingLabels = await _client.GetLabelsAsync();

        if (existingLabels.Contains(name, StringComparer.InvariantCultureIgnoreCase))
        {
            return;
        }
        
        _logger.LogDebug("Creating category {name}", name);
        
        await _dryRunInterceptor.InterceptAsync(CreateLabel, name);
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
        
        foreach (UTorrentItem download in downloads.Cast<UTorrentItem>())
        {
            if (string.IsNullOrEmpty(download.Hash) || string.IsNullOrEmpty(download.Name) || string.IsNullOrEmpty(download.Label))
            {
                continue;
            }
            
            if (excludedHashes.Any(x => x.Equals(download.Hash, StringComparison.InvariantCultureIgnoreCase)))
            {
                _logger.LogDebug("skip | download is used by an arr | {name}", download.Name);
                continue;
            }

            var properties = await _client.GetTorrentPropertiesAsync(download.Hash);
        
            if (ignoredDownloads.Count > 0 &&
                (download.ShouldIgnore(ignoredDownloads) || properties.TrackerList.Any(x => x.ShouldIgnore(ignoredDownloads))))
            {
                _logger.LogInformation("skip | download is ignored | {name}", download.Name);
                continue;
            }
        
            ContextProvider.Set("downloadName", download.Name);
            ContextProvider.Set("hash", download.Hash);
            
            List<UTorrentFile>? files = await _client.GetTorrentFilesAsync(download.Hash);
            
            bool hasHardlinks = false;
            
            foreach (var file in files ?? [])
            {
                string filePath = string.Join(Path.DirectorySeparatorChar, Path.Combine(download.SavePath, file.Name).Split(['\\', '/']));

                if (file.Priority <= 0)
                {
                    _logger.LogDebug("skip | file is not downloaded | {file}", filePath);
                    continue;
                }
                
                long hardlinkCount = _hardLinkFileService
                    .GetHardLinkCount(filePath, !string.IsNullOrEmpty(downloadCleanerConfig.UnlinkedIgnoredRootDir));
        
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
            
            //TODO change label on download object
            await _dryRunInterceptor.InterceptAsync(ChangeLabel, download.Hash, downloadCleanerConfig.UnlinkedTargetCategory);
            
            await _eventPublisher.PublishCategoryChanged(download.Label, downloadCleanerConfig.UnlinkedTargetCategory);
            
            _logger.LogInformation("category changed for {name}", download.Name);
            
            download.Label = downloadCleanerConfig.UnlinkedTargetCategory;
        }
    }

    /// <inheritdoc/>
    public override async Task DeleteDownload(string hash)
    {
        hash = hash.ToLowerInvariant();
        
        await _client.RemoveTorrentsAsync([hash]);
    }

    protected async Task CreateLabel(string name)
    {
        await UTorrentClient.CreateLabel(name);
    }
    
    protected virtual async Task ChangeLabel(string hash, string newLabel)
    {
        await _client.SetTorrentLabelAsync(hash, newLabel);
    }
} 