using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.Extensions.Logging;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

public partial class QBitService
{
    /// <inheritdoc/>
    public override async Task<List<ITorrentItem>?> GetSeedingDownloads()
    {
        var torrentList = await _client.GetTorrentListAsync(new TorrentListQuery { Filter = TorrentListFilter.Completed });
        if (torrentList is null)
        {
            return null;
        }

        var result = new List<ITorrentItem>();
        foreach (var torrent in torrentList.Where(x => !string.IsNullOrEmpty(x.Hash)))
        {
            var trackers = await GetTrackersAsync(torrent.Hash!);
            var properties = await _client.GetTorrentPropertiesAsync(torrent.Hash!);
            bool isPrivate = properties?.AdditionalData.TryGetValue("is_private", out var dictValue) == true &&
                           bool.TryParse(dictValue?.ToString(), out bool boolValue) && boolValue;

            result.Add(new QBitItem(torrent, trackers, isPrivate));
        }

        return result;
    }

    /// <inheritdoc/>
    public override List<ITorrentItem>? FilterDownloadsToBeCleanedAsync(List<ITorrentItem>? downloads, List<CleanCategory> categories) =>
        downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => categories.Any(cat => cat.Name.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

    /// <inheritdoc/>
    public override List<ITorrentItem>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItem>? downloads, List<string> categories)
    {
        var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));

        return downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .Where(x =>
            {
                if (downloadCleanerConfig.UnlinkedUseTag)
                {
                    return !x.Tags.Any(tag =>
                        tag.Equals(downloadCleanerConfig.UnlinkedTargetCategory, StringComparison.InvariantCultureIgnoreCase));
                }

                return true;
            })
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
                _logger.LogInformation("skip | download is ignored | {name}", download.Name);
                continue;
            }

            CleanCategory? category = categoriesToClean
                .FirstOrDefault(x => (download.Category ?? string.Empty).Equals(x.Name, StringComparison.InvariantCultureIgnoreCase));

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

            SeedingCheckResult result = ShouldCleanDownload(download.Ratio, TimeSpan.FromSeconds(download.SeedingTimeSeconds), category);

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

            await _eventPublisher.PublishDownloadCleaned(download.Ratio, TimeSpan.FromSeconds(download.SeedingTimeSeconds), category.Name, result.Reason);
        }
    }

    public override async Task CreateCategoryAsync(string name)
    {
        IReadOnlyDictionary<string, Category>? existingCategories = await _client.GetCategoriesAsync();

        if (existingCategories.Any(x => x.Value.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
        {
            return;
        }
        
        _logger.LogDebug("Creating category {name}", name);

        await _dryRunInterceptor.InterceptAsync(CreateCategory, name);
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
                _logger.LogInformation("skip | download is ignored | {name}", download.Name);
                continue;
            }

            // Get the underlying TorrentInfo to access SavePath and files
            TorrentInfo? torrentInfo = await _client.GetTorrentListAsync(new TorrentListQuery { Hashes = new[] { download.Hash } })
                .ContinueWith(t => t.Result?.FirstOrDefault());

            if (torrentInfo is null)
            {
                _logger.LogDebug("failed to find torrent info for {name}", download.Name);
                continue;
            }

            IReadOnlyList<TorrentContent>? files = await _client.GetTorrentContentsAsync(download.Hash);

            if (files is null)
            {
                _logger.LogDebug("failed to find files for {name}", download.Name);
                continue;
            }

            ContextProvider.Set("downloadName", download.Name);
            ContextProvider.Set("hash", download.Hash);
            bool hasHardlinks = false;

            foreach (TorrentContent file in files)
            {
                if (!file.Index.HasValue)
                {
                    _logger.LogDebug("skip | file index is null for {name}", download.Name);
                    hasHardlinks = true;
                    break;
                }

                string filePath = string.Join(Path.DirectorySeparatorChar, Path.Combine(torrentInfo.SavePath, file.Name).Split(['\\', '/']));

                if (file.Priority is TorrentContentPriority.Skip)
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
            }
        }
    }

    /// <inheritdoc/>
    public override async Task DeleteDownload(string hash)
    {
        await _client.DeleteAsync([hash], deleteDownloadedData: true);
    }

    protected async Task CreateCategory(string name)
    {
        await _client.AddCategoryAsync(name);
    }
    
    protected virtual async Task ChangeCategory(string hash, string newCategory)
    {
        var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));
        
        if (downloadCleanerConfig.UnlinkedUseTag)
        {
            await _client.AddTorrentTagAsync([hash], newCategory);
            return;
        }

        await _client.SetTorrentCategoryAsync([hash], newCategory);
    }
}