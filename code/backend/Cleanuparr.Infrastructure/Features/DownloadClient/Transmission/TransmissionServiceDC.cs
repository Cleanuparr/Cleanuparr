using Cleanuparr.Domain.Entities;
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
    public override List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<ISeedingRule> seedingRules)
    {
        return downloads
            ?.Where(x => seedingRules
                .Any(cat => cat.Name.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase))
            )
            .ToList();
    }

    public override List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig)
    {
        return downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => unlinkedConfig.Categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task DeleteDownload(ITorrentItemWrapper torrent, bool deleteSourceFiles)
    {
        var transmissionTorrent = (TransmissionItemWrapper)torrent;
        await _client.TorrentRemoveAsync([transmissionTorrent.Info.Id], deleteSourceFiles);
    }

    public override async Task CreateCategoryAsync(string name)
    {
        await Task.CompletedTask;
    }

    public override async Task ChangeCategoryForNoHardLinksAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }

        foreach (TransmissionItemWrapper torrent in downloads.Cast<TransmissionItemWrapper>())
        {
            if (string.IsNullOrEmpty(torrent.Hash) || string.IsNullOrEmpty(torrent.Name) || string.IsNullOrEmpty(torrent.Info.DownloadDir))
            {
                continue;
            }

            ContextProvider.Set(ContextProvider.Keys.ItemName, torrent.Name);
            ContextProvider.Set(ContextProvider.Keys.Hash, torrent.Hash);
            ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, _downloadClientConfig.ExternalOrInternalUrl);
            ContextProvider.Set(ContextProvider.Keys.DownloadClientType, _downloadClientConfig.TypeName);
            ContextProvider.Set(ContextProvider.Keys.DownloadClientName, _downloadClientConfig.Name);

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

                if (!string.IsNullOrEmpty(unlinkedConfig.DownloadDirectorySource) &&
                    !string.IsNullOrEmpty(unlinkedConfig.DownloadDirectoryTarget))
                {
                    filePath = filePath.Replace(unlinkedConfig.DownloadDirectorySource, unlinkedConfig.DownloadDirectoryTarget);
                }

                long hardlinkCount = _hardLinkFileService.GetHardLinkCount(filePath, unlinkedConfig.IgnoredRootDirs.Count > 0);

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
            string newLocation = torrent.Info.GetNewLocationByAppend(unlinkedConfig.TargetCategory);

            await _dryRunInterceptor.InterceptAsync(ChangeDownloadLocation, torrent.Info.Id, newLocation);

            _logger.LogInformation("category changed for {name}", torrent.Name);

            await _eventPublisher.PublishCategoryChanged(currentCategory, unlinkedConfig.TargetCategory);

            torrent.Category = unlinkedConfig.TargetCategory;
        }
    }

    protected virtual async Task ChangeDownloadLocation(long downloadId, string newLocation)
    {
        await _client.TorrentSetLocationAsync([downloadId], newLocation, true);
    }
}
