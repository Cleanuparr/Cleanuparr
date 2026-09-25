using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

public partial class DelugeService
{
    public override async Task<List<ITorrentItemWrapper>> GetSeedingDownloads()
    {
        var downloads = await _client.GetStatusForAllTorrents();
        if (downloads is null)
        {
            return [];
        }

        return downloads
            .Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => x.State is DelugeState.Seeding || x is { IsFinished: true, State: DelugeState.Paused or DelugeState.Queued })
            .Select(ITorrentItemWrapper (x) => new DelugeItemWrapper(x))
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task<List<ITorrentItemWrapper>> GetAllTorrentsLite()
    {
        List<DownloadStatus>? downloads = await _client.GetStatusForAllTorrents();
        if (downloads is null)
        {
            throw new DelugeClientException("Deluge returned no torrent status");
        }

        List<ITorrentItemWrapper> torrents = downloads
            .Where(x => !string.IsNullOrEmpty(x.Hash))
            .Select(ITorrentItemWrapper (x) => new DelugeItemWrapper(x))
            .ToList();

        ThrowIfTorrentListCollapsed(downloads.Count, torrents.Count);

        return torrents;
    }

    /// <inheritdoc/>
    public override Task<IReadOnlyList<string>> GetClaimedPathsAsync(IReadOnlyList<ITorrentItemWrapper> torrents) =>
        BuildClaimedPathsAsync(torrents, async torrent =>
        {
            if (string.IsNullOrEmpty(torrent.Hash))
            {
                return [];
            }

            DelugeContents? contents = await _client.GetTorrentFiles(torrent.Hash);
            List<string> relativePaths = [];
            ProcessFiles(contents?.Contents, (_, file) =>
            {
                if (!string.IsNullOrEmpty(file.Path))
                {
                    relativePaths.Add(file.Path);
                }
            });
            return relativePaths;
        });

    /// <inheritdoc/>
    public override async Task DeleteDownload(ITorrentItemWrapper torrent, bool deleteSourceFiles)
    {
        string hash = torrent.Hash.ToLowerInvariant();

        await _client.DeleteTorrents([hash], deleteSourceFiles);
    }

    public override async Task StopDownload(ITorrentItemWrapper torrent)
    {
        string hash = torrent.Hash.ToLowerInvariant();

        await _client.PauseTorrents([hash]);
    }

    public override async Task CreateCategoryAsync(string name)
    {
        IReadOnlyList<string> existingLabels = await _client.GetLabels();

        if (existingLabels.Contains(name, StringComparer.InvariantCultureIgnoreCase))
        {
            return;
        }

        _logger.LogDebug("Creating category {name}", name);

        await _dryRunInterceptor.InterceptAsync(() => CreateLabel(name));
    }

    /// <inheritdoc/>
    protected override async Task<IEnumerable<(string FilePath, HardLinkScanAction Action)>?> GetHardLinkScanItemsAsync(ITorrentItemWrapper torrent)
    {
        DelugeItemWrapper delugeTorrent = (DelugeItemWrapper)torrent;
        DelugeContents? contents;

        try
        {
            contents = await _client.GetTorrentFiles(delugeTorrent.Hash);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "failed to find torrent files for {Name}", delugeTorrent.Name);
            return null;
        }

        List<(string FilePath, HardLinkScanAction Action)> scanItems = [];

        ProcessFiles(contents?.Contents, (_, file) =>
        {
            string filePath = PathHelper.NormalizeAndRemap(
                Path.Combine(delugeTorrent.Info.DownloadLocation, file.Path),
                _downloadClientConfig.DownloadDirectorySource,
                _downloadClientConfig.DownloadDirectoryTarget);

            HardLinkScanAction action = file.Priority <= 0
                ? HardLinkScanAction.SkipUnwanted
                : HardLinkScanAction.CheckHardLinks;

            scanItems.Add((filePath, action));
        });

        return scanItems;
    }

    /// <inheritdoc/>
    protected override Task ChangeCategoryInClientAsync(ITorrentItemWrapper torrent, string targetCategory, bool useTag) =>
        ChangeLabel(torrent.Hash, targetCategory);

    protected async Task CreateLabel(string name)
    {
        await _client.CreateLabel(name);
    }

    protected virtual async Task ChangeLabel(string hash, string newLabel)
    {
        await _client.SetTorrentLabel(hash, newLabel);
    }
}
