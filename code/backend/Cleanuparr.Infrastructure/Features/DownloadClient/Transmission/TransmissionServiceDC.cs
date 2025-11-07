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
    public override async Task<List<ITorrentItemWrapper>> GetSeedingDownloads()
    {
        var result = await _client.TorrentGetAsync(Fields);
        return result?.Torrents
            ?.Where(x => !string.IsNullOrEmpty(x.HashString))
            .Where(x => x.Status is 5 or 6)
            .Select(ITorrentItemWrapper (x) => new TransmissionItemWrapper(x))
            .ToList() ?? [];
    }

    /// <inheritdoc/>
    public override List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<CleanCategory> categories)
    {
        return downloads
            ?.Where(x => categories
                .Any(cat => cat.Name.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase))
            )
            .ToList();
    }

    public override List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, List<string> categories)
    {
        return downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task CleanDownloadsAsync(List<ITorrentItemWrapper>? downloads, List<CleanCategory> categoriesToClean)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }

        foreach (TransmissionItemWrapper torrent in downloads.Cast<TransmissionItemWrapper>())
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

            await _dryRunInterceptor.InterceptAsync(RemoveDownloadAsync, torrent.Info.Id);

            _logger.LogInformation(
                "download cleaned | {reason} reached | {name}",
                result.Reason is CleanReason.MaxRatioReached
                    ? "max ratio & min seeding time"
                    : "max seeding time",
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

        foreach (TransmissionItemWrapper torrent in downloads.Cast<TransmissionItemWrapper>())
        {
            if (string.IsNullOrEmpty(torrent.Hash) || string.IsNullOrEmpty(torrent.Name) || string.IsNullOrEmpty(torrent.Info.DownloadDir))
            {
                continue;
            }
            
            ContextProvider.Set("downloadName", torrent.Name);
            ContextProvider.Set("hash", torrent.Hash);

            if (torrent.Info.Files is null || torrent.Info.FileStats is null)
            {
                _logger.LogDebug("skip | download has no files | {name}", torrent.Name);
                continue;
            }
            
            bool hasHardlinks = false;
            bool hasErrors = false;

            for (int i = 0; i < torrent.Info.Files.Length; i++)
            {
                TransmissionTorrentFiles file = torrent.Info.Files[i];
                TransmissionTorrentFileStats stats = torrent.Info.FileStats[i];

                if (stats.Wanted is null or false || string.IsNullOrEmpty(file.Name))
                {
                    continue;
                }

                string filePath = string.Join(Path.DirectorySeparatorChar, Path.Combine(torrent.Info.DownloadDir, file.Name).Split(['\\', '/']));

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

            string currentCategory = torrent.Category ?? string.Empty;
            string newLocation = string.Join(Path.DirectorySeparatorChar, Path.Combine(torrent.Info.DownloadDir, downloadCleanerConfig.UnlinkedTargetCategory).Split(['\\', '/']));

            await _dryRunInterceptor.InterceptAsync(ChangeDownloadLocation, torrent.Info.Id, newLocation);

            _logger.LogInformation("category changed for {name}", torrent.Name);

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