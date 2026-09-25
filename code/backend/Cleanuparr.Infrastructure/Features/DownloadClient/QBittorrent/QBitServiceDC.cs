using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

public partial class QBitService
{
    /// <inheritdoc/>
    public override async Task<List<ITorrentItemWrapper>> GetSeedingDownloads()
    {
        var torrentList = await _client.GetTorrentListAsync(new TorrentListQuery { Filter = TorrentListFilter.Completed });
        if (torrentList is null)
        {
            return [];
        }

        var result = new List<ITorrentItemWrapper>();
        foreach (var torrent in torrentList.Where(x => !string.IsNullOrEmpty(x.Hash)))
        {
            IReadOnlyList<TorrentTracker>? trackers = await GetTrackersAsync(torrent.Hash);
            TorrentProperties? properties = await _client.GetTorrentPropertiesAsync(torrent.Hash);

            if (trackers is null || properties is null)
            {
                _logger.LogDebug("skip | torrent no longer exists in the download client | {Name}", torrent.Name);
                continue;
            }

            bool isPrivate = properties.AdditionalData.TryGetValue("is_private", out JToken? dictValue) &&
                           bool.TryParse(dictValue?.ToString(), out bool boolValue) && boolValue;

            result.Add(new QBitItemWrapper(torrent, trackers, isPrivate));
        }

        return result;
    }

    /// <inheritdoc/>
    public override async Task<List<ITorrentItemWrapper>> GetAllTorrentsLite()
    {
        IReadOnlyList<TorrentInfo>? torrentList = await _client.GetTorrentListAsync(new TorrentListQuery());
        if (torrentList is null)
        {
            throw new InvalidOperationException("qBittorrent returned no torrent list");
        }

        List<ITorrentItemWrapper> torrents = torrentList
            .Where(x => !string.IsNullOrEmpty(x.Hash))
            .Select(ITorrentItemWrapper (t) => new QBitItemWrapper(t, [], false))
            .ToList();

        ThrowIfTorrentListCollapsed(torrentList.Count, torrents.Count);

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

            IReadOnlyList<TorrentContent>? files = await _client.GetTorrentContentsAsync(torrent.Hash);
            return files?.Select(f => f.Name).Where(name => !string.IsNullOrEmpty(name)).ToList() ?? [];
        });

    /// <inheritdoc/>
    public override List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<ISeedingRule> seedingRules) =>
        downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => seedingRules.Any(rule => rule.Categories.Any(cat => cat.Equals(x.Category, StringComparison.OrdinalIgnoreCase))))
            .ToList();

    /// <inheritdoc/>
    public override List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig)
    {
        return downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => unlinkedConfig.Categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .Where(x =>
            {
                if (unlinkedConfig.UseTag && x is QBitItemWrapper qBitItemWrapper)
                {
                    return !qBitItemWrapper.Tags.Any(tag =>
                        tag.Equals(unlinkedConfig.TargetCategory, StringComparison.InvariantCultureIgnoreCase));
                }

                return true;
            })
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task DeleteDownload(ITorrentItemWrapper torrent, bool deleteSourceFiles)
    {
        await _client.DeleteAsync([torrent.Hash], deleteSourceFiles);
    }

    /// <inheritdoc/>
    public override async Task StopDownload(ITorrentItemWrapper torrent)
    {
        await _client.PauseAsync([torrent.Hash]);
    }

    public override async Task CreateCategoryAsync(string name)
    {
        IReadOnlyDictionary<string, Category>? existingCategories = await _client.GetCategoriesAsync();

        if (existingCategories.Any(x => x.Value.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
        {
            return;
        }

        _logger.LogDebug("Creating category {Name}", name);

        await _dryRunInterceptor.InterceptAsync(() => CreateCategory(name));
    }

    /// <inheritdoc/>
    protected override async Task<IEnumerable<(string FilePath, HardLinkScanAction Action)>?> GetHardLinkScanItemsAsync(ITorrentItemWrapper torrent)
    {
        QBitItemWrapper qBitTorrent = (QBitItemWrapper)torrent;
        IReadOnlyList<TorrentContent>? files = await _client.GetTorrentContentsAsync(qBitTorrent.Hash);

        if (files is null)
        {
            _logger.LogDebug("failed to find files for {Name}", qBitTorrent.Name);
            return null;
        }

        IEnumerable<(string FilePath, HardLinkScanAction Action)> BuildScanItems()
        {
            foreach (TorrentContent file in files)
            {
                if (!file.Index.HasValue)
                {
                    _logger.LogDebug("skip | file index is null for {Name}", qBitTorrent.Name);
                    yield return (string.Empty, HardLinkScanAction.TreatAsLinked);
                    yield break;
                }

                string filePath = PathHelper.NormalizeAndRemap(
                    Path.Combine(qBitTorrent.Info.SavePath, file.Name),
                    _downloadClientConfig.DownloadDirectorySource,
                    _downloadClientConfig.DownloadDirectoryTarget);

                yield return (filePath, file.Priority is TorrentContentPriority.Skip
                    ? HardLinkScanAction.SkipUnwanted
                    : HardLinkScanAction.CheckHardLinks);
            }
        }

        return BuildScanItems();
    }

    /// <inheritdoc/>
    protected override bool SupportsTags => true;

    /// <inheritdoc/>
    protected override async Task ChangeCategoryInClientAsync(ITorrentItemWrapper torrent, string targetCategory, bool useTag) =>
        await ChangeCategory(torrent.Hash, targetCategory, useTag);

    protected async Task CreateCategory(string name)
    {
        await _client.AddCategoryAsync(name);
    }

    protected virtual async Task ChangeCategory(string hash, string newCategory, bool useTag)
    {
        if (useTag)
        {
            await _client.AddTorrentTagAsync([hash], newCategory);
            return;
        }

        await _client.SetTorrentCategoryAsync([hash], newCategory);
    }
}
