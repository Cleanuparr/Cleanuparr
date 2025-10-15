using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RdtClient;

public partial class RdtService
{
    /// <inheritdoc/>
    public override async Task<List<object>?> GetSeedingDownloads()
    {
        var torrentList = await GetTorrentListAsync(filter: "completed");
        return torrentList?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Cast<object>()
            .ToList();
    }

    /// <inheritdoc/>
    public override List<object>? FilterDownloadsToBeCleanedAsync(List<object>? downloads, List<CleanCategory> categories) =>
        downloads
            ?.Cast<RdtTorrentInfo>()
            .Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => categories.Any(cat => cat.Name.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .Cast<object>()
            .ToList();

    /// <inheritdoc/>
    public override List<object>? FilterDownloadsToChangeCategoryAsync(List<object>? downloads, List<string> categories)
    {
        var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));

        return downloads
            ?.Cast<RdtTorrentInfo>()
            .Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .Where(x =>
            {
                if (downloadCleanerConfig.UnlinkedUseTag)
                {
                    if (!string.IsNullOrEmpty(x.Tags))
                    {
                        var tags = x.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        return !tags.Any(tag => tag.Equals(downloadCleanerConfig.UnlinkedTargetCategory, StringComparison.InvariantCultureIgnoreCase));
                    }
                    return true;
                }

                return true;
            })
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

        foreach (RdtTorrentInfo download in downloads)
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

            // RDT-Client doesn't support the trackers endpoint
            if (ignoredDownloads.Count > 0 && download.ShouldIgnore(ignoredDownloads))
            {
                _logger.LogInformation("skip | download is ignored | {name}", download.Name);
                continue;
            }

            CleanCategory? category = categoriesToClean
                .FirstOrDefault(x => download.Category.Equals(x.Name, StringComparison.InvariantCultureIgnoreCase));

            if (category is null)
            {
                continue;
            }
            
            var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));

            // RDT-Client doesn't support GetTorrentPropertiesAsync, so we skip private checking
            if (!downloadCleanerConfig.DeletePrivate)
            {
                _logger.LogDebug("skip | DeletePrivate is disabled (note: rdt-client doesn't support private torrent detection) | {name}", download.Name);
                continue;
            }

            ContextProvider.Set("downloadName", download.Name);
            ContextProvider.Set("hash", download.Hash);

            SeedingCheckResult result = ShouldCleanDownload(download.Ratio, download.SeedingTime ?? TimeSpan.Zero, category);

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

            await _eventPublisher.PublishDownloadCleaned(download.Ratio, download.SeedingTime ?? TimeSpan.Zero, category.Name, result.Reason);
        }
    }

    public override async Task CreateCategoryAsync(string name)
    {
        Dictionary<string, RdtCategory>? existingCategories = await GetCategoriesAsync();

        if (existingCategories.Any(x => x.Value.Name?.Equals(name, StringComparison.InvariantCultureIgnoreCase) ?? false))
        {
            return;
        }

        _logger.LogDebug("Creating category {name}", name);

        await _dryRunInterceptor.InterceptAsync(CreateCategory, name);
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

        foreach (RdtTorrentInfo download in downloads)
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

            // RDT-Client doesn't support the trackers endpoint
            if (ignoredDownloads.Count > 0 && download.ShouldIgnore(ignoredDownloads))
            {
                _logger.LogInformation("skip | download is ignored | {name}", download.Name);
                continue;
            }

            List<RdtTorrentContent>? files = await GetTorrentContentsAsync(download.Hash);

            if (files is null)
            {
                _logger.LogDebug("failed to find files for {name}", download.Name);
                continue;
            }

            ContextProvider.Set("downloadName", download.Name);
            ContextProvider.Set("hash", download.Hash);
            bool hasHardlinks = false;

            foreach (RdtTorrentContent file in files)
            {
                if (!file.Index.HasValue)
                {
                    _logger.LogDebug("skip | file index is null for {name}", download.Name);
                    hasHardlinks = true;
                    break;
                }

                string filePath = string.Join(Path.DirectorySeparatorChar, Path.Combine(download.SavePath, file.Name).Split(['\\', '/']));

                if (file.Priority == 0) // Priority 0 means file is not downloaded
                {
                    _logger.LogDebug("skip | file is not downloaded | {file}", filePath);
                    continue;
                }

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

            await _dryRunInterceptor.InterceptAsync(ChangeCategory, download.Hash, downloadCleanerConfig.UnlinkedTargetCategory);
            
            await _eventPublisher.PublishCategoryChanged(download.Category, downloadCleanerConfig.UnlinkedTargetCategory, downloadCleanerConfig.UnlinkedUseTag);

            if (downloadCleanerConfig.UnlinkedUseTag)
            {
                _logger.LogInformation("tag added for {name}", download.Name);
            }
            else
            {
                _logger.LogInformation("category changed for {name}", download.Name);
                download.Category = downloadCleanerConfig.UnlinkedTargetCategory;
            }
        }
    }

    /// <inheritdoc/>
    public override async Task DeleteDownload(string hash)
    {
        await DeleteAsync([hash], deleteFiles: true);
    }

    protected async Task CreateCategory(string name)
    {
        await AddCategoryAsync(name);
    }

    protected virtual async Task ChangeCategory(string hash, string newCategory)
    {
        var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));

        if (downloadCleanerConfig.UnlinkedUseTag)
        {
            // RDT-Client doesn't support the addTags endpoint
            // Fall back to category change instead
            _logger.LogWarning("RDT-Client doesn't support tags, using category change instead");
        }

        await SetTorrentCategoryAsync([hash], newCategory);
    }
}