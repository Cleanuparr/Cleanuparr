using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.RTorrent.Response;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;

public partial class RTorrentService
{
    public override async Task<List<ITorrentItemWrapper>> GetSeedingDownloads()
    {
        var downloads = await _client.GetAllTorrentsAsync();

        var result = new List<ITorrentItemWrapper>();
        foreach (var torrent in downloads
            .Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => x is { Complete: 1, State: 1 }))
        {
            var trackers = await _client.GetTrackersAsync(torrent.Hash);
            result.Add(new RTorrentItemWrapper(torrent, trackers, _timeProvider));
        }

        return result;
    }

    /// <inheritdoc/>
    public override async Task<List<ITorrentItemWrapper>> GetAllTorrentsLite()
    {
        List<RTorrentTorrent> downloads = await _client.GetAllTorrentsAsync();

        List<ITorrentItemWrapper> torrents = downloads
            .Where(x => !string.IsNullOrEmpty(x.Hash))
            .Select(ITorrentItemWrapper (x) => new RTorrentItemWrapper(x, null, _timeProvider))
            .ToList();

        ThrowIfTorrentListCollapsed(downloads.Count, torrents.Count);

        return torrents;
    }

    /// <inheritdoc/>
    public override Task<IReadOnlyList<string>> GetClaimedPathsAsync(IReadOnlyList<ITorrentItemWrapper> torrents)
    {
        HashSet<string> claimed = new(StringComparer.OrdinalIgnoreCase);

        foreach (ITorrentItemWrapper torrent in torrents)
        {
            if (torrent is not RTorrentItemWrapper wrapper)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(wrapper.Info.BasePath))
            {
                claimed.Add(RemapAndTrim(wrapper.Info.BasePath));
            }

            if (!string.IsNullOrEmpty(wrapper.Info.Directory))
            {
                claimed.Add(RemapAndTrim(wrapper.Info.Directory));
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(claimed.ToList());
    }

    public override List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<ISeedingRule> seedingRules) =>
        downloads
            ?.Where(x => seedingRules.Any(rule => rule.Categories.Any(cat => cat.Equals(x.Category, StringComparison.OrdinalIgnoreCase))))
            .ToList();

    public override List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig) =>
        downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => unlinkedConfig.Categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

    /// <inheritdoc/>
    public override async Task DeleteDownload(ITorrentItemWrapper torrent, bool deleteSourceFiles)
    {
        string hash = torrent.Hash.ToUpperInvariant();
        await _client.DeleteTorrentAsync(hash);

        if (deleteSourceFiles)
        {
            string savePath = PathHelper.NormalizeAndRemap(
                torrent.SavePath,
                _downloadClientConfig.DownloadDirectorySource,
                _downloadClientConfig.DownloadDirectoryTarget);

            if (!TryDeleteFiles(savePath, true))
            {
                _logger.LogWarning("Failed to delete files | {name}", torrent.Name);
            }
        }
    }

    /// <inheritdoc/>
    public override async Task StopDownload(ITorrentItemWrapper torrent)
    {
        string hash = torrent.Hash.ToUpperInvariant();
        await _client.StopTorrentAsync(hash);
    }

    /// <summary>
    /// rTorrent doesn't have native category management. Labels are stored in d.custom1
    /// and are created implicitly when set. This is a no-op.
    /// </summary>
    public override Task CreateCategoryAsync(string name)
    {
        return Task.CompletedTask;
    }

    public override async Task ChangeCategoryForNoHardLinksAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }

        foreach (RTorrentItemWrapper torrent in downloads.Cast<RTorrentItemWrapper>())
        {
            if (string.IsNullOrEmpty(torrent.Hash) || string.IsNullOrEmpty(torrent.Name) || string.IsNullOrEmpty(torrent.Category))
            {
                continue;
            }

            ContextProvider.Set(ContextProvider.Keys.ItemName, torrent.Name);
            ContextProvider.Set(ContextProvider.Keys.Hash, torrent.Hash);
            SetDownloadClientContext();

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

            IEnumerable<(string FilePath, HardLinkScanAction Action)> BuildScanItems()
            {
                foreach (RTorrentFile file in files)
                {
                    string filePath = PathHelper.NormalizeAndRemap(
                        Path.Combine(torrent.Info.Directory ?? torrent.Info.BasePath ?? "", file.Path),
                        _downloadClientConfig.DownloadDirectorySource,
                        _downloadClientConfig.DownloadDirectoryTarget);

                    yield return (filePath, file.Priority <= 0
                        ? HardLinkScanAction.SkipUnwanted
                        : HardLinkScanAction.CheckHardLinks);
                }
            }

            (bool hasHardlinks, bool hasErrors) = ScanForHardLinks(BuildScanItems(), unlinkedConfig.IgnoredRootDirs.Count > 0);

            if (hasErrors)
            {
                continue;
            }

            if (hasHardlinks)
            {
                _logger.LogDebug("skip | download has hardlinks | {name}", torrent.Name);
                continue;
            }

            await _dryRunInterceptor.InterceptAsync(() => ChangeLabel(torrent.Hash, unlinkedConfig.TargetCategory));

            _logger.LogInformation("category changed for {name}", torrent.Name);

            await _eventPublisher.PublishCategoryChanged(torrent.Category, unlinkedConfig.TargetCategory);

            torrent.Category = unlinkedConfig.TargetCategory;
        }
    }

    /// <inheritdoc/>
    public override async Task ChangeTorrentCategoryAsync(ITorrentItemWrapper torrent, string targetCategory, bool useTag)
    {
        ContextProvider.Set(ContextProvider.Keys.ItemName, torrent.Name);
        ContextProvider.Set(ContextProvider.Keys.Hash, torrent.Hash);
        SetDownloadClientContext();

        string currentCategory = torrent.Category ?? string.Empty;

        await _dryRunInterceptor.InterceptAsync(() => ChangeLabel(torrent.Hash, targetCategory));

        await _eventPublisher.PublishCategoryChanged(currentCategory, targetCategory);

        torrent.Category = targetCategory;
    }

    protected virtual async Task ChangeLabel(string hash, string newLabel)
    {
        await _client.SetLabelAsync(hash, newLabel);
    }
}
