using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Shared.Helpers;
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

    public override List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<ISeedingRule> seedingRules) =>
        downloads
            ?.Where(x => seedingRules.Any(cat => cat.Name.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

    public override List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig) =>
        downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => unlinkedConfig.Categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

    /// <inheritdoc/>
    public override async Task DeleteDownload(ITorrentItemWrapper torrent, bool deleteSourceFiles)
    {
        string hash = torrent.Hash.ToLowerInvariant();
        await _client.RemoveTorrentsAsync([hash], deleteSourceFiles);
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

        foreach (UTorrentItemWrapper torrent in downloads.Cast<UTorrentItemWrapper>())
        {
            if (string.IsNullOrEmpty(torrent.Hash) || string.IsNullOrEmpty(torrent.Name) || string.IsNullOrEmpty(torrent.Category))
            {
                continue;
            }

            ContextProvider.Set(ContextProvider.Keys.ItemName, torrent.Name);
            ContextProvider.Set(ContextProvider.Keys.Hash, torrent.Hash);
            ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, _downloadClientConfig.ExternalOrInternalUrl);
            ContextProvider.Set(ContextProvider.Keys.DownloadClientId, _downloadClientConfig.Id);
            ContextProvider.Set(ContextProvider.Keys.DownloadClientType, _downloadClientConfig.TypeName);
            ContextProvider.Set(ContextProvider.Keys.DownloadClientName, _downloadClientConfig.Name);

            List<UTorrentFile>? files = await _client.GetTorrentFilesAsync(torrent.Hash);

            bool hasHardlinks = false;
            bool hasErrors = false;

            foreach (var file in files ?? [])
            {
                string filePath = string.Join(Path.DirectorySeparatorChar, Path.Combine(torrent.Info.SavePath, file.Name).Split(['\\', '/']));

                filePath = PathHelper.RemapPath(filePath, unlinkedConfig.DownloadDirectorySource, unlinkedConfig.DownloadDirectoryTarget);

                if (file.Priority <= 0)
                {
                    _logger.LogDebug("skip | file is not downloaded | {file}", filePath);
                    continue;
                }

                long hardlinkCount = _hardLinkFileService
                    .GetHardLinkCount(filePath, unlinkedConfig.IgnoredRootDirs.Count > 0);

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

            await _dryRunInterceptor.InterceptAsync(ChangeLabel, torrent.Hash, unlinkedConfig.TargetCategory);

            await _eventPublisher.PublishCategoryChanged(torrent.Category, unlinkedConfig.TargetCategory);

            _logger.LogInformation("category changed for {name}", torrent.Name);

            torrent.Category = unlinkedConfig.TargetCategory;
        }
    }

    protected virtual async Task ChangeLabel(string hash, string newLabel)
    {
        await _client.SetTorrentLabelAsync(hash, newLabel);
    }
}
