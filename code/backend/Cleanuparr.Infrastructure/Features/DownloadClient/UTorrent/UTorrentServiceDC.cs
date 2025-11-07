using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

public partial class UTorrentService
{
    public override async Task<List<ITorrentItemWrapper>> GetSeedingDownloads()
    {
        var torrents = await _client.GetTorrentsAsync();
        var result = new List<ITorrentItemWrapper>();

        foreach (UTorrentItem torrent in torrents.Where(x => !string.IsNullOrEmpty(x.Hash) && x.IsSeeding()))
        {
            var properties = await _client.GetTorrentPropertiesAsync(torrent.Hash);
            result.Add(new UTorrentItemWrapper(torrent, properties));
        }

        return result;
    }

    public override List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<CleanCategory> categories) =>
        downloads
            ?.Where(x => categories.Any(cat => cat.Name.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

    public override List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, List<string> categories) =>
        downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

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
                .FirstOrDefault(x => x.Name.Equals(torrent.Category, StringComparison.InvariantCultureIgnoreCase));

            if (category is null)
            {
                continue;
            }

            var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));

            if (!downloadCleanerConfig.DeletePrivate && torrent.IsPrivate)
            {
                _logger.LogDebug("skip | torrent is private | {name}", torrent.Name);
                continue;
            }

            ContextProvider.Set("downloadName", torrent.Name);
            ContextProvider.Set("hash", torrent.Hash);

            TimeSpan seedingTime = TimeSpan.FromSeconds(torrent.SeedingTimeSeconds);
            SeedingCheckResult result = ShouldCleanDownload(torrent.Ratio, seedingTime, category);

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

            await _eventPublisher.PublishDownloadCleaned(torrent.Ratio, seedingTime, category.Name, result.Reason);
        }
    }

    public override async Task CreateCategoryAsync(string name)
    {
        await Task.CompletedTask;
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

        foreach (UTorrentItemWrapper torrent in downloads.Cast<UTorrentItemWrapper>())
        {
            if (string.IsNullOrEmpty(torrent.Hash) || string.IsNullOrEmpty(torrent.Name) || string.IsNullOrEmpty(torrent.Category))
            {
                continue;
            }
            
            ContextProvider.Set("downloadName", torrent.Name);
            ContextProvider.Set("hash", torrent.Hash);

            List<UTorrentFile>? files = await _client.GetTorrentFilesAsync(torrent.Hash);

            bool hasHardlinks = false;
            bool hasErrors = false;

            foreach (var file in files ?? [])
            {
                string filePath = string.Join(Path.DirectorySeparatorChar, Path.Combine(torrent.Info.SavePath, file.Name).Split(['\\', '/']));

                if (file.Priority <= 0)
                {
                    _logger.LogDebug("skip | file is not downloaded | {file}", filePath);
                    continue;
                }

                long hardlinkCount = _hardLinkFileService
                    .GetHardLinkCount(filePath, !string.IsNullOrEmpty(downloadCleanerConfig.UnlinkedIgnoredRootDir));

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

            await _dryRunInterceptor.InterceptAsync(ChangeLabel, torrent.Hash, downloadCleanerConfig.UnlinkedTargetCategory);

            await _eventPublisher.PublishCategoryChanged(torrent.Category, downloadCleanerConfig.UnlinkedTargetCategory);

            _logger.LogInformation("category changed for {name}", torrent.Name);
        }
    }

    /// <inheritdoc/>
    public override async Task DeleteDownload(string hash)
    {
        hash = hash.ToLowerInvariant();
        
        await _client.RemoveTorrentsAsync([hash]);
    }
    
    protected virtual async Task ChangeLabel(string hash, string newLabel)
    {
        await _client.SetTorrentLabelAsync(hash, newLabel);
    }
} 