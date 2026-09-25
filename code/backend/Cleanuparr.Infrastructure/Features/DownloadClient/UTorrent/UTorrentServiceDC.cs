using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
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
            UTorrentProperties? properties = await _client.GetTorrentPropertiesAsync(torrent.Hash);

            if (properties is null)
            {
                _logger.LogDebug("skip | torrent no longer exists in the download client | {Name}", torrent.Name);
                continue;
            }

            result.Add(new UTorrentItemWrapper(torrent, properties, _timeProvider));
        }

        return result;
    }

    /// <inheritdoc/>
    public override async Task<List<ITorrentItemWrapper>> GetAllTorrentsLite()
    {
        List<UTorrentItem> reported = await _client.GetTorrentsAsync();

        List<ITorrentItemWrapper> torrents = reported
            .Where(x => !string.IsNullOrEmpty(x.Hash))
            .Select(ITorrentItemWrapper (x) => new UTorrentItemWrapper(x, new UTorrentProperties(), _timeProvider))
            .ToList();

        ThrowIfTorrentListCollapsed(reported.Count, torrents.Count);

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

            List<UTorrentFile>? files = await _client.GetTorrentFilesAsync(torrent.Hash);
            return files?.Select(f => f.Name).Where(name => !string.IsNullOrEmpty(name)).ToList() ?? [];
        });

    /// <inheritdoc/>
    public override async Task DeleteDownload(ITorrentItemWrapper torrent, bool deleteSourceFiles)
    {
        string hash = torrent.Hash.ToLowerInvariant();
        await _client.RemoveTorrentsAsync([hash], deleteSourceFiles);
    }

    public override async Task StopDownload(ITorrentItemWrapper torrent)
    {
        string hash = torrent.Hash.ToLowerInvariant();
        await _client.StopTorrentsAsync([hash]);
    }

    public override async Task CreateCategoryAsync(string name)
    {
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override async Task<IEnumerable<(string FilePath, HardLinkScanAction Action)>?> GetHardLinkScanItemsAsync(ITorrentItemWrapper torrent)
    {
        UTorrentItemWrapper uTorrent = (UTorrentItemWrapper)torrent;
        List<UTorrentFile>? files = await _client.GetTorrentFilesAsync(uTorrent.Hash);

        List<(string FilePath, HardLinkScanAction Action)> scanItems = [];

        foreach (UTorrentFile file in files ?? [])
        {
            string filePath = PathHelper.NormalizeAndRemap(
                Path.Combine(uTorrent.Info.SavePath, file.Name),
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
        ChangeLabel(torrent.Hash, targetCategory);

    protected virtual async Task ChangeLabel(string hash, string newLabel)
    {
        await _client.SetTorrentLabelAsync(hash, newLabel);
    }
}
