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

    public override List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<SeedingRule> seedingRules) =>
        downloads
            ?.Where(x => seedingRules.Any(cat => cat.Name.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

    public override List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, List<string> categories) =>
        downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

    /// <inheritdoc/>
    protected override async Task DeleteDownloadInternal(ITorrentItemWrapper torrent, bool deleteSourceFiles)
    {
        await DeleteDownload(torrent.Hash, deleteSourceFiles);
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

        foreach (UTorrentItemWrapper torrent in downloads.Cast<UTorrentItemWrapper>())
        {
            if (string.IsNullOrEmpty(torrent.Hash) || string.IsNullOrEmpty(torrent.Name) || string.IsNullOrEmpty(torrent.Category))
            {
                continue;
            }
            
            ContextProvider.Set(ContextProvider.Keys.DownloadName, torrent.Name);
            ContextProvider.Set(ContextProvider.Keys.Hash, torrent.Hash);
            ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, _downloadClientConfig.ExternalOrInternalUrl);

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
                    .GetHardLinkCount(filePath, downloadCleanerConfig.UnlinkedIgnoredRootDirs.Count > 0);

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
            
            torrent.Category = downloadCleanerConfig.UnlinkedTargetCategory;
        }
    }

    /// <inheritdoc/>
    public override async Task DeleteDownload(string hash, bool deleteSourceFiles)
    {
        hash = hash.ToLowerInvariant();

        await _client.RemoveTorrentsAsync([hash], deleteSourceFiles);
    }
    
    protected virtual async Task ChangeLabel(string hash, string newLabel)
    {
        await _client.SetTorrentLabelAsync(hash, newLabel);
    }
} 