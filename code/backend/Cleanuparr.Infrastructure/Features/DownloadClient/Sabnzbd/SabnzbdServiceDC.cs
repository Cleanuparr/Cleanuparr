using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Sabnzbd;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;

public partial class SabnzbdService
{
    /// <inheritdoc/>
    public override async Task<List<IDownloadItem>> GetAllDownloadsLite()
    {
        Task<SabnzbdQueueData?> queueTask = _client.GetQueueAsync();
        Task<SabnzbdHistoryData?> historyTask = _client.GetHistoryAsync();
        await Task.WhenAll(queueTask, historyTask);

        SabnzbdQueueData? queue = await queueTask;
        SabnzbdHistoryData? history = await historyTask;

        if (queue is null || history is null)
        {
            throw new InvalidOperationException("SABnzbd returned no queue/history");
        }

        List<IDownloadItem> items = [];

        foreach (SabnzbdQueueSlot slot in queue.Slots)
        {
            if (string.IsNullOrEmpty(slot.NzoId))
            {
                continue;
            }

            items.Add(new SabnzbdItemWrapper(slot));
        }

        foreach (SabnzbdHistorySlot slot in history.Slots)
        {
            if (string.IsNullOrEmpty(slot.NzoId))
            {
                continue;
            }

            items.Add(new SabnzbdItemWrapper(slot));
        }

        ThrowIfTorrentListCollapsed(queue.Slots.Count + history.Slots.Count, items.Count);

        return items;
    }

    /// <summary>
    /// Claims each history job's storage path and every ancestor folder above it, plus, while the client is busy
    /// (queued or post-processing), every top-level entry under SABnzbd's incomplete <c>download_dir</c>.
    /// </summary>
    /// <inheritdoc/>
    public override async Task<IReadOnlyList<string>> GetClaimedPathsAsync(IReadOnlyList<IDownloadItem> downloads)
    {
        HashSet<string> claimed = new(BuildHistoryClaims(downloads), StringComparer.OrdinalIgnoreCase);

        bool clientIsBusy = downloads.Any(t => t is SabnzbdItemWrapper { IsInHistory: false } or SabnzbdItemWrapper { Status: not ("Completed" or "Failed") });

        if (clientIsBusy)
        {
            foreach (string claim in await BuildIncompleteClaimsAsync())
            {
                claimed.Add(claim);
            }
        }

        return claimed.ToList();
    }

    /// <summary>
    /// Claims a history job's <c>storage</c> path plus every ancestor folder above it. A single-file job's
    /// <c>storage</c> names the file itself, one level under its job folder; a category subfolder or an absolute
    /// category path moves the job's top-level entry further up than one hop. Claiming the whole ancestor chain
    /// covers all three without needing to know <c>complete_dir</c>.
    /// </summary>
    private IEnumerable<string> BuildHistoryClaims(IReadOnlyList<IDownloadItem> downloads)
    {
        return downloads
            .Where(x => !string.IsNullOrEmpty(x.SavePath))
            .SelectMany(x => PathAndAncestors(RemapAndTrim(x.SavePath)));
    }

    private static IEnumerable<string> PathAndAncestors(string path)
    {
        for (string? current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            yield return current;
        }
    }

    /// <summary>
    /// SAB sanitizes job names for incomplete folders and appends ".1"/".2" on a clash, so download_dir plus the job name can miss the real folder.
    /// Claiming every entry while the client is busy (queued or post-processing) protects active downloads instead; an idle client claims nothing, so leftovers stay cleanable.
    /// A busy client that can't resolve a usable download_dir throws instead, since silently claiming nothing would let the orphan scan move an in-progress job.
    /// </summary>
    private async Task<IReadOnlyList<string>> BuildIncompleteClaimsAsync()
    {
        string? downloadDir = await _client.GetDownloadDirAsync();

        if (string.IsNullOrEmpty(downloadDir))
        {
            throw new InvalidOperationException("SABnzbd download_dir is empty, skipping orphan scan while jobs are active");
        }

        string remappedDir = RemapAndTrim(downloadDir);

        if (!Path.IsPathRooted(remappedDir))
        {
            throw new InvalidOperationException($"SABnzbd download_dir '{downloadDir}' is relative, skipping orphan scan while jobs are active");
        }

        if (!Directory.Exists(remappedDir))
        {
            throw new InvalidOperationException($"SABnzbd download_dir '{remappedDir}' does not exist, skipping orphan scan while jobs are active");
        }

        return Directory.EnumerateFileSystemEntries(remappedDir).ToList();
    }

    /// <summary>
    /// History delete always archives off (<c>archive=0</c> on the client call), so the job disappears for good.
    /// SABnzbd's own <c>del_files</c> only deletes a <b>Failed</b> job's files; for a Completed job we delete the
    /// storage folder ourselves, the same way Sonarr does for its own usenet clients.
    /// </summary>
    /// <inheritdoc/>
    public override async Task DeleteDownload(IDownloadItem item, bool deleteSourceFiles)
    {
        SabnzbdItemWrapper sabnzbdItem = (SabnzbdItemWrapper)item;

        if (!sabnzbdItem.IsInHistory)
        {
            await _client.DeleteFromQueueAsync(item.DownloadId, deleteSourceFiles);
            return;
        }

        await _client.DeleteFromHistoryAsync(item.DownloadId, deleteSourceFiles);

        if (!deleteSourceFiles || sabnzbdItem.Status != "Completed")
        {
            return;
        }

        if (string.IsNullOrEmpty(sabnzbdItem.SavePath))
        {
            _logger.LogDebug("skip disk delete | no storage path | {Name}", sabnzbdItem.Name);
            return;
        }

        string storagePath = RemapAndTrim(sabnzbdItem.SavePath);

        // A single-file job reports `storage` as the file itself, one level under its job folder.
        if (File.Exists(storagePath))
        {
            storagePath = Path.GetDirectoryName(storagePath) ?? storagePath;
        }

        await _dryRunInterceptor.InterceptAsync(() =>
        {
            TryDeleteFiles(storagePath, failOnNotFound: false);
            return Task.CompletedTask;
        });
    }
}
