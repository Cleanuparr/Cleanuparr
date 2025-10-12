using Cleanuparr.Domain.Entities;
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
    public override async Task<List<ITorrentItem>?> GetSeedingDownloads()
    {
        var torrents = await _client.GetTorrentsAsync();
        var result = new List<ITorrentItem>();

        foreach (var torrent in torrents.Where(x => !string.IsNullOrEmpty(x.Hash) && x.IsSeeding()))
        {
            var properties = await _client.GetTorrentPropertiesAsync(torrent.Hash);
            result.Add(new UTorrentItemWrapper(torrent, properties));
        }

        return result;
    }

    public override List<ITorrentItem>? FilterDownloadsToBeCleanedAsync(List<ITorrentItem>? downloads, List<CleanCategory> categories) =>
        downloads
            ?.Where(x => categories.Any(cat => cat.Name.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

    public override List<ITorrentItem>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItem>? downloads, List<string> categories) =>
        downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

    /// <inheritdoc/>
    public override async Task CleanDownloadsAsync(List<ITorrentItem>? downloads, List<CleanCategory> categoriesToClean, HashSet<string> excludedHashes,
        IReadOnlyList<string> ignoredDownloads)
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

            await _dryRunInterceptor.InterceptAsync(DeleteDownload, download.Hash);

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
        var existingLabels = await _client.GetLabelsAsync();

        if (existingLabels.Contains(name, StringComparer.InvariantCultureIgnoreCase))
        {
            return;
        }
        
        _logger.LogDebug("Creating category {name}", name);
        
        await _dryRunInterceptor.InterceptAsync(CreateLabel, name);
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
            if (string.IsNullOrEmpty(download.Hash) || string.IsNullOrEmpty(download.Name) || string.IsNullOrEmpty(download.Category))
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

            ContextProvider.Set("downloadName", download.Name);
            ContextProvider.Set("hash", download.Hash);

            // Get the underlying UTorrentItem to access SavePath
            UTorrentItem? torrentItem = await _client.GetTorrentAsync(download.Hash);
            if (torrentItem is null)
            {
                _logger.LogDebug("failed to find torrent for {name}", download.Name);
                continue;
            }

            List<UTorrentFile>? files = await _client.GetTorrentFilesAsync(download.Hash);

            bool hasHardlinks = false;

            foreach (var file in files ?? [])
            {
                string filePath = string.Join(Path.DirectorySeparatorChar, Path.Combine(torrentItem.SavePath, file.Name).Split(['\\', '/']));

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

            await _dryRunInterceptor.InterceptAsync(ChangeLabel, download.Hash, downloadCleanerConfig.UnlinkedTargetCategory);

            await _eventPublisher.PublishCategoryChanged(download.Category, downloadCleanerConfig.UnlinkedTargetCategory);

            _logger.LogInformation("category changed for {name}", download.Name);
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