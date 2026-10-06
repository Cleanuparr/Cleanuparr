using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Sabnzbd;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;

public partial class SabnzbdService
{
    /// <summary>
    /// Usenet downloads do not seed - there is nothing for the seeding rules to evaluate.
    /// </summary>
    /// <inheritdoc/>
    public override Task<List<ITorrentItemWrapper>> GetSeedingDownloads() =>
        Task.FromResult(new List<ITorrentItemWrapper>());

    /// <inheritdoc/>
    public override async Task<List<ITorrentItemWrapper>> GetAllTorrentsLite()
    {
        SabnzbdQueueData? queue = await _client.GetQueueAsync();
        SabnzbdHistoryData? history = await _client.GetHistoryAsync();

        List<ITorrentItemWrapper> items = [];

        foreach (SabnzbdQueueSlot slot in queue?.Slots ?? [])
        {
            if (string.IsNullOrEmpty(slot.NzoId))
            {
                continue;
            }

            items.Add(new SabnzbdItemWrapper(slot, queue?.KbPerSec ?? 0, queue?.Paused ?? false));
        }

        foreach (SabnzbdHistorySlot slot in history?.Slots ?? [])
        {
            if (string.IsNullOrEmpty(slot.NzoId))
            {
                continue;
            }

            items.Add(new SabnzbdItemWrapper(slot));
        }

        return items;
    }

    /// <inheritdoc/>
    public override Task<IReadOnlyList<string>> GetClaimedPathsAsync(IReadOnlyList<ITorrentItemWrapper> torrents) =>
        BuildClaimedPathsAsync(torrents, _ => Task.FromResult<IReadOnlyCollection<string>>([]));

    /// <inheritdoc/>
    public override async Task DeleteDownload(ITorrentItemWrapper torrent, bool deleteSourceFiles)
    {
        SabnzbdItemWrapper sabnzbdItem = (SabnzbdItemWrapper)torrent;

        if (sabnzbdItem.IsInHistory)
        {
            await _client.DeleteFromHistoryAsync(torrent.Hash, deleteSourceFiles);
            return;
        }

        await _client.DeleteFromQueueAsync(torrent.Hash, deleteSourceFiles);
    }

    /// <inheritdoc/>
    public override async Task StopDownload(ITorrentItemWrapper torrent)
    {
        await _client.PauseAsync(torrent.Hash);
    }

    /// <summary>
    /// SABnzbd categories are normally defined in its own config; the API offers no documented, version-stable
    /// way to create one, so this only warns when the category is missing instead of failing the run.
    /// </summary>
    /// <inheritdoc/>
    public override async Task CreateCategoryAsync(string name)
    {
        IReadOnlyList<string> existingCategories = await _client.GetCategoriesAsync();

        if (existingCategories.Contains(name, StringComparer.InvariantCultureIgnoreCase))
        {
            return;
        }

        _logger.LogWarning(
            "Category {Name} does not exist in SABnzbd and cannot be created automatically; create it in SABnzbd's own settings first | {ClientName}",
            name, _downloadClientConfig.Name);
    }

    /// <inheritdoc/>
    protected override Task<IEnumerable<(string FilePath, HardLinkScanAction Action)>?> GetHardLinkScanItemsAsync(ITorrentItemWrapper torrent)
    {
        SabnzbdItemWrapper sabnzbdItem = (SabnzbdItemWrapper)torrent;

        if (string.IsNullOrEmpty(sabnzbdItem.SavePath) || !Directory.Exists(sabnzbdItem.SavePath))
        {
            return Task.FromResult<IEnumerable<(string FilePath, HardLinkScanAction Action)>?>(null);
        }

        IEnumerable<(string FilePath, HardLinkScanAction Action)> BuildScanItems()
        {
            foreach (string file in Directory.EnumerateFiles(sabnzbdItem.SavePath, "*", SearchOption.AllDirectories))
            {
                string filePath = PathHelper.NormalizeAndRemap(
                    file,
                    _downloadClientConfig.DownloadDirectorySource,
                    _downloadClientConfig.DownloadDirectoryTarget);

                yield return (filePath, HardLinkScanAction.CheckHardLinks);
            }
        }

        return Task.FromResult<IEnumerable<(string FilePath, HardLinkScanAction Action)>?>(BuildScanItems());
    }

    /// <inheritdoc/>
    protected override async Task ChangeCategoryInClientAsync(ITorrentItemWrapper torrent, string targetCategory, bool useTag) =>
        await _client.ChangeCategoryAsync(torrent.Hash, targetCategory);
}
