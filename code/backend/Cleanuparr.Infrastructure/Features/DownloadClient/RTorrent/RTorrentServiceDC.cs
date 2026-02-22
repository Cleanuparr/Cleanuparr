using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.RTorrent.Response;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;

public partial class RTorrentService
{
    public override async Task<List<ITorrentItemWrapper>> GetSeedingDownloads()
    {
        var downloads = await _client.GetAllTorrentsAsync();

        return downloads
            .Where(x => !string.IsNullOrEmpty(x.Hash))
            // Seeding: complete=1 (finished) and state=1 (started)
            .Where(x => x is { Complete: 1, State: 1 })
            .Select(ITorrentItemWrapper (x) => new RTorrentItemWrapper(x))
            .ToList();
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
    public override async Task DeleteDownload(ITorrentItemWrapper torrent, bool deleteSourceFiles)
    {
        string hash = torrent.Hash.ToUpperInvariant();
        await _client.DeleteTorrentAsync(hash);
        
        if (deleteSourceFiles)
        {
            if (!TryDeleteFiles(torrent.SavePath, true))
            {
                _logger.LogWarning("Failed to delete files | {name}", torrent.Name);
            }
        }
    }

    /// <summary>
    /// rTorrent doesn't have native category management. Labels are stored in d.custom1
    /// and are created implicitly when set. This is a no-op.
    /// </summary>
    public override Task CreateCategoryAsync(string name)
    {
        return Task.CompletedTask;
    }

    public override async Task ChangeCategoryForNoHardLinksAsync(List<ITorrentItemWrapper>? downloads)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }

        var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));

        foreach (RTorrentItemWrapper torrent in downloads.Cast<RTorrentItemWrapper>())
        {
            if (string.IsNullOrEmpty(torrent.Hash) || string.IsNullOrEmpty(torrent.Name) || string.IsNullOrEmpty(torrent.Category))
            {
                continue;
            }

            ContextProvider.Set(ContextProvider.Keys.ItemName, torrent.Name);
            ContextProvider.Set(ContextProvider.Keys.Hash, torrent.Hash);
            ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, _downloadClientConfig.ExternalOrInternalUrl);
            ContextProvider.Set(ContextProvider.Keys.DownloadClientType, _downloadClientConfig.TypeName);
            ContextProvider.Set(ContextProvider.Keys.DownloadClientName, _downloadClientConfig.Name);

            List<RTorrentFile> files;
            try
            {
                files = await _client.GetTorrentFilesAsync(torrent.Hash);
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "failed to find torrent files for {name}", torrent.Name);
                continue;
            }

            bool hasHardlinks = false;
            bool hasErrors = false;

            foreach (var file in files)
            {
                string filePath = string.Join(Path.DirectorySeparatorChar,
                    Path.Combine(torrent.Info.BasePath ?? "", file.Path).Split(['\\', '/']));

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
                    continue;
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

            _logger.LogInformation("category changed for {name}", torrent.Name);

            await _eventPublisher.PublishCategoryChanged(torrent.Category, downloadCleanerConfig.UnlinkedTargetCategory);

            torrent.Category = downloadCleanerConfig.UnlinkedTargetCategory;
        }
    }

    protected virtual async Task ChangeLabel(string hash, string newLabel)
    {
        await _client.SetLabelAsync(hash, newLabel);
    }
}
