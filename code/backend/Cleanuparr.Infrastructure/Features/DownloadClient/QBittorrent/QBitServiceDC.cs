using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.Extensions.Logging;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

public partial class QBitService
{
    /// <inheritdoc/>
    public override async Task<List<ITorrentItemWrapper>> GetSeedingDownloads()
    {
        var torrentList = await _client.GetTorrentListAsync(new TorrentListQuery { Filter = TorrentListFilter.Completed });
        if (torrentList is null)
        {
            return [];
        }

        var result = new List<ITorrentItemWrapper>();
        foreach (var torrent in torrentList.Where(x => !string.IsNullOrEmpty(x.Hash)))
        {
            var trackers = await GetTrackersAsync(torrent.Hash!);
            var properties = await _client.GetTorrentPropertiesAsync(torrent.Hash!);
            bool isPrivate = properties?.AdditionalData.TryGetValue("is_private", out var dictValue) == true &&
                           bool.TryParse(dictValue?.ToString(), out bool boolValue) && boolValue;

            result.Add(new QBitItemWrapper(torrent, trackers, isPrivate));
        }

        return result;
    }

    /// <inheritdoc/>
    public override List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<CleanCategory> categories) =>
        downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => categories.Any(cat => cat.Name.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

    /// <inheritdoc/>
    public override List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, List<string> categories)
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
    public override async Task CleanDownloadsAsync(List<ITorrentItemWrapper>? downloads, List<CleanCategory> categoriesToClean)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }

        foreach (ITorrentItemWrapper torrent in downloads)
        {
            if (string.IsNullOrEmpty(torrent.Hash))
            {
                continue;
            }
            
            CleanCategory? category = categoriesToClean
                .FirstOrDefault(x => (torrent.Category ?? string.Empty).Equals(x.Name, StringComparison.InvariantCultureIgnoreCase));

            if (category is null)
            {
                continue;
            }

            var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));

            if (!downloadCleanerConfig.DeletePrivate && torrent.IsPrivate)
            {
                _logger.LogDebug("skip | download is private | {name}", torrent.Name);
                continue;
            }

            ContextProvider.Set("downloadName", torrent.Name);
            ContextProvider.Set("hash", torrent.Hash);

            SeedingCheckResult result = ShouldCleanDownload(torrent.Ratio, TimeSpan.FromSeconds(torrent.SeedingTimeSeconds), category);

            if (!result.ShouldClean)
            {
                continue;
            }

            await _dryRunInterceptor.InterceptAsync(DeleteDownload, torrent.Hash);

            _logger.LogInformation(
                "download cleaned | {reason} reached | {name}",
                result.Reason is CleanReason.MaxRatioReached
                    ? "MAX_RATIO & MIN_SEED_TIME"
                    : "MAX_SEED_TIME",
                torrent.Name
            );

            await _eventPublisher.PublishDownloadCleaned(torrent.Ratio, TimeSpan.FromSeconds(torrent.SeedingTimeSeconds), category.Name, result.Reason);
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

    public override async Task ChangeCategoryForNoHardLinksAsync(List<ITorrentItemWrapper>? downloads)
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

        foreach (QBitItemWrapper torrent in downloads.Cast<QBitItemWrapper>())
        {
            if (string.IsNullOrEmpty(torrent.Hash))
            {
                continue;
            }
            
            IReadOnlyList<TorrentContent>? files = await _client.GetTorrentContentsAsync(torrent.Hash);

            if (files is null)
            {
                _logger.LogDebug("failed to find files for {name}", torrent.Name);
                continue;
            }

            ContextProvider.Set("downloadName", torrent.Name);
            ContextProvider.Set("hash", torrent.Hash);
            bool hasHardlinks = false;
            bool hasErrors = false;

            foreach (TorrentContent file in files)
            {
                if (!file.Index.HasValue)
                {
                    _logger.LogDebug("skip | file index is null for {name}", torrent.Name);
                    hasHardlinks = true;
                    break;
                }

                string filePath = string.Join(Path.DirectorySeparatorChar, Path.Combine(torrent.Info.SavePath, file.Name).Split(['\\', '/']));

                if (file.Priority is TorrentContentPriority.Skip)
                {
                    _logger.LogDebug("skip | file is not downloaded | {file}", filePath);
                    continue;
                }

                long hardlinkCount = _hardLinkFileService.GetHardLinkCount(filePath, !string.IsNullOrEmpty(downloadCleanerConfig.UnlinkedIgnoredRootDir));

                if (hardlinkCount < 0)
                {
                    _logger.LogError("skip | file does not exist or insufficient permissions | {file}", filePath);
                    hasErrors = true;
                    break;
                }

                if (hardlinkCount > 0)
                {
                    hasHardlinks = true;
                    break;
                }
            }

            if (hasErrors)
            {
                continue;
            }

            if (hasHardlinks)
            {
                _logger.LogDebug("skip | download has hardlinks | {name}", torrent.Name);
                continue;
            }

            await _dryRunInterceptor.InterceptAsync(ChangeCategory, torrent.Hash, downloadCleanerConfig.UnlinkedTargetCategory);

            await _eventPublisher.PublishCategoryChanged(torrent.Category, downloadCleanerConfig.UnlinkedTargetCategory, downloadCleanerConfig.UnlinkedUseTag);

            if (downloadCleanerConfig.UnlinkedUseTag)
            {
                _logger.LogInformation("tag added for {name}", torrent.Name);
            }
            else
            {
                _logger.LogInformation("category changed for {name}", torrent.Name);
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