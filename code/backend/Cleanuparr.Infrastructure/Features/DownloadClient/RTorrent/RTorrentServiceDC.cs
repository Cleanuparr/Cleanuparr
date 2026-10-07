using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.RTorrent.Response;
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
    public override async Task<List<IDownloadItem>> GetAllDownloadsLite()
    {
        List<RTorrentTorrent> downloads = await _client.GetAllTorrentsAsync();

        List<IDownloadItem> torrents = downloads
            .Where(x => !string.IsNullOrEmpty(x.Hash))
            .Select(IDownloadItem (x) => new RTorrentItemWrapper(x, null, _timeProvider))
            .ToList();

        ThrowIfTorrentListCollapsed(downloads.Count, torrents.Count);

        return torrents;
    }

    /// <inheritdoc/>
    public override Task<IReadOnlyList<string>> GetClaimedPathsAsync(IReadOnlyList<IDownloadItem> torrents)
    {
        HashSet<string> claimed = new(StringComparer.OrdinalIgnoreCase);

        foreach (IDownloadItem torrent in torrents)
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

    /// <inheritdoc/>
    public override async Task DeleteDownload(IDownloadItem torrent, bool deleteSourceFiles)
    {
        string hash = torrent.DownloadId.ToUpperInvariant();
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
    public override async Task StopDownload(IDownloadItem torrent)
    {
        string hash = torrent.DownloadId.ToUpperInvariant();
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

    /// <inheritdoc/>
    protected override async Task<IEnumerable<(string FilePath, HardLinkScanAction Action)>?> GetHardLinkScanItemsAsync(ITorrentItemWrapper torrent)
    {
        RTorrentItemWrapper rTorrent = (RTorrentItemWrapper)torrent;
        List<RTorrentFile> files;

        try
        {
            files = await _client.GetTorrentFilesAsync(rTorrent.DownloadId);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "failed to find torrent files for {Name}", rTorrent.Name);
            return null;
        }

        List<(string FilePath, HardLinkScanAction Action)> scanItems = [];

        foreach (RTorrentFile file in files)
        {
            string filePath = PathHelper.NormalizeAndRemap(
                Path.Combine(rTorrent.Info.Directory ?? rTorrent.Info.BasePath ?? "", file.Path),
                _downloadClientConfig.DownloadDirectorySource,
                _downloadClientConfig.DownloadDirectoryTarget);

            scanItems.Add((filePath, file.Priority <= 0
                ? HardLinkScanAction.SkipUnwanted
                : HardLinkScanAction.CheckHardLinks));
        }

        return scanItems;
    }

    /// <inheritdoc/>
    protected override Task ChangeCategoryInClientAsync(ITorrentItemWrapper torrent, string targetCategory, bool useTag) =>
        ChangeLabel(torrent.DownloadId, targetCategory);

    protected virtual async Task ChangeLabel(string hash, string newLabel)
    {
        await _client.SetLabelAsync(hash, newLabel);
    }
}
