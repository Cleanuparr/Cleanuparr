using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Transmission.API.RPC.Arguments;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

public partial class TransmissionService
{
    public override async Task<List<ITorrentItemWrapper>> GetSeedingDownloads()
    {
        var result = await _client.TorrentGetAsync(Fields);
        return result?.Torrents
            ?.Where(x => !string.IsNullOrEmpty(x.HashString))
            .Where(x => x.Status is 5 or 6 || x is { IsFinished: true, Status: 0 })
            .Select(ITorrentItemWrapper (x) => new TransmissionItemWrapper(x))
            .ToList() ?? [];
    }

    /// <inheritdoc/>
    public override async Task<List<ITorrentItemWrapper>> GetAllTorrentsLite()
    {
        TransmissionTorrents? result = await _client.TorrentGetAsync(Fields);
        if (result?.Torrents is null)
        {
            throw new InvalidOperationException("Transmission returned no torrent list");
        }

        List<ITorrentItemWrapper> torrents = result.Torrents
            .Where(x => !string.IsNullOrEmpty(x.HashString))
            .Select(ITorrentItemWrapper (x) => new TransmissionItemWrapper(x))
            .ToList();

        ThrowIfTorrentListCollapsed(result.Torrents.Length, torrents.Count);

        return torrents;
    }

    /// <inheritdoc/>
    public override Task<IReadOnlyList<string>> GetClaimedPathsAsync(IReadOnlyList<ITorrentItemWrapper> torrents) =>
        BuildClaimedPathsAsync(torrents, torrent =>
        {
            IReadOnlyCollection<string> files = torrent is TransmissionItemWrapper { Info.Files.Length: > 0 } wrapper
                ? wrapper.Info.Files
                    .Select(f => f.Name)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList()
                : [];
            return Task.FromResult(files);
        });

    /// <inheritdoc/>
    public override List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig)
    {
        return downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => unlinkedConfig.Categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .Where(x =>
            {
                if (unlinkedConfig.UseTag)
                {
                    return !x.Tags.Any(tag =>
                        tag.Equals(unlinkedConfig.TargetCategory, StringComparison.InvariantCultureIgnoreCase));
                }

                return true;
            })
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task DeleteDownload(ITorrentItemWrapper torrent, bool deleteSourceFiles)
    {
        var transmissionTorrent = (TransmissionItemWrapper)torrent;
        await _client.TorrentRemoveAsync([transmissionTorrent.Info.Id], deleteSourceFiles);
    }

    /// <inheritdoc/>
    public override async Task StopDownload(ITorrentItemWrapper torrent)
    {
        TransmissionItemWrapper transmissionTorrent = (TransmissionItemWrapper)torrent;
        await _client.TorrentStopAsync([transmissionTorrent.Info.Id]);
    }

    public override async Task CreateCategoryAsync(string name)
    {
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override Task<IEnumerable<(string FilePath, HardLinkScanAction Action)>?> GetHardLinkScanItemsAsync(ITorrentItemWrapper torrent)
    {
        TransmissionItemWrapper transmissionTorrent = (TransmissionItemWrapper)torrent;

        if (string.IsNullOrEmpty(transmissionTorrent.Info.DownloadDir))
        {
            return Task.FromResult<IEnumerable<(string FilePath, HardLinkScanAction Action)>?>(null);
        }

        if (transmissionTorrent.Info.Files is null || transmissionTorrent.Info.FileStats is null)
        {
            _logger.LogDebug("skip | download has no files | {name}", transmissionTorrent.Name);
            return Task.FromResult<IEnumerable<(string FilePath, HardLinkScanAction Action)>?>(null);
        }

        IEnumerable<(string FilePath, HardLinkScanAction Action)> BuildScanItems()
        {
            for (int i = 0; i < transmissionTorrent.Info.Files.Length; i++)
            {
                TransmissionTorrentFiles file = transmissionTorrent.Info.Files[i];
                TransmissionTorrentFileStats stats = transmissionTorrent.Info.FileStats[i];

                if (stats.Wanted is null or false || string.IsNullOrEmpty(file.Name))
                {
                    continue;
                }

                string filePath = PathHelper.NormalizeAndRemap(
                    Path.Combine(transmissionTorrent.Info.DownloadDir, file.Name),
                    _downloadClientConfig.DownloadDirectorySource,
                    _downloadClientConfig.DownloadDirectoryTarget);

                yield return (filePath, HardLinkScanAction.CheckHardLinks);
            }
        }

        return Task.FromResult<IEnumerable<(string FilePath, HardLinkScanAction Action)>?>(BuildScanItems());
    }

    /// <inheritdoc/>
    protected override bool SupportsTags => true;

    /// <inheritdoc/>
    protected override async Task ChangeCategoryInClientAsync(ITorrentItemWrapper torrent, string targetCategory, bool useTag)
    {
        TransmissionItemWrapper transmissionTorrent = (TransmissionItemWrapper)torrent;

        if (useTag)
        {
            string[] newLabels = transmissionTorrent.Tags
                .Append(targetCategory)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            await ChangeLabels(transmissionTorrent.Info.Id, newLabels);

            return;
        }

        string newLocation = transmissionTorrent.Info.GetNewLocationByAppend(targetCategory);

        await ChangeDownloadLocation(transmissionTorrent.Info.Id, newLocation);
    }

    protected virtual async Task ChangeDownloadLocation(long downloadId, string newLocation)
    {
        await _client.TorrentSetLocationAsync([downloadId], newLocation, true);
    }

    protected virtual async Task ChangeLabels(long downloadId, string[] labels)
    {
        await _client.TorrentSetAsync(new TorrentSettings
        {
            Ids = [downloadId],
            Labels = labels,
        });
    }
}
