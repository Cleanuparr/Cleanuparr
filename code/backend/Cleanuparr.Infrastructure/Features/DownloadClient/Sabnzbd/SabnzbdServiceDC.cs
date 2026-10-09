using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Sabnzbd;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;

public partial class SabnzbdService
{
    /// <inheritdoc/>
    public override async Task<List<IDownloadItem>> GetAllDownloadsLite()
    {
        SabnzbdQueueData? queue = await _client.GetQueueAsync();
        SabnzbdHistoryData? history = await _client.GetHistoryAsync();

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
    /// Claims each history job's storage path, SABnzbd's own <c>download_dir</c> and <c>complete_dir</c>, plus,
    /// while the client is busy (queued or post-processing), every top-level entry under <c>download_dir</c>.
    /// </summary>
    /// <inheritdoc/>
    public override async Task<IReadOnlyList<string>> GetClaimedPathsAsync(IReadOnlyList<IDownloadItem> downloads)
    {
        HashSet<string> claimed = new(BuildHistoryClaims(downloads), StringComparer.OrdinalIgnoreCase);

        bool clientIsBusy = downloads.Any(t => t is SabnzbdItemWrapper { IsInHistory: false } or SabnzbdItemWrapper { Status: not ("Completed" or "Failed") });

        foreach (string claim in await ClaimConfiguredDirectoriesAsync())
        {
            claimed.Add(claim);
        }

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
    /// Claims a history job's <c>storage</c> path. The scanner keeps every folder above a claim.
    /// </summary>
    private IEnumerable<string> BuildHistoryClaims(IReadOnlyList<IDownloadItem> downloads)
    {
        return downloads
            .Where(x => !string.IsNullOrEmpty(x.SavePath))
            .Select(x => RemapAndTrim(x.SavePath));
    }

    /// <summary>
    /// Claims <c>download_dir</c> and <c>complete_dir</c> when each resolves to an existing folder, and skips one that doesn't.
    /// For a busy client, <see cref="BuildIncompleteClaimsAsync"/> throws on an unresolvable <c>download_dir</c> instead.
    /// </summary>
    private async Task<IReadOnlyList<string>> ClaimConfiguredDirectoriesAsync()
    {
        List<string> claims = [];

        foreach (Func<Task<string?>> getDirAsync in new Func<Task<string?>>[] { _client.GetDownloadDirAsync, _client.GetCompleteDirAsync })
        {
            string? dir = await TryResolveExistingDirAsync(getDirAsync);
            if (dir is not null)
            {
                claims.Add(dir);
            }
        }

        return claims;
    }

    private async Task<string?> TryResolveExistingDirAsync(Func<Task<string?>> getDirAsync)
    {
        string? dir = await getDirAsync();

        if (string.IsNullOrEmpty(dir))
        {
            return null;
        }

        string remapped = RemapAndTrim(dir);
        return Path.IsPathRooted(remapped) && Directory.Exists(remapped) ? remapped : null;
    }

    /// <summary>
    /// SAB sanitizes job names for incomplete folders and appends ".1"/".2" on a clash, so download_dir plus the job name can miss the real folder.
    /// Claiming every entry while the client is busy (queued or post-processing) protects active downloads instead; an idle client claims none of them, so leftovers stay cleanable.
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
        bool isSingleFileJob = File.Exists(storagePath);

        await _dryRunInterceptor.InterceptAsync(() =>
        {
            TryDeleteFiles(storagePath, failOnNotFound: false);

            if (isSingleFileJob)
            {
                TryDeleteEmptyJobFolder(Path.GetDirectoryName(storagePath), sabnzbdItem.Name);
            }

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Removes a single-file job's folder once it is empty.
    /// Only a folder named after the job qualifies, so complete_dir and category folders stay.
    /// The ".1"/".2" suffix SAB adds on a clash still matches.
    /// </summary>
    private void TryDeleteEmptyJobFolder(string? folder, string jobName)
    {
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(jobName))
        {
            return;
        }

        if (!Path.GetFileName(folder).StartsWith(jobName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Directory.Exists(folder) || Directory.EnumerateFileSystemEntries(folder).Any())
        {
            return;
        }

        try
        {
            Directory.Delete(folder);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete empty job folder | {Folder}", folder);
        }
    }
}
