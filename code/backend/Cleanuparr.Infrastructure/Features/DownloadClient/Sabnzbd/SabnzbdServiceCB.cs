using Cleanuparr.Domain.Entities.Sabnzbd;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;

public partial class SabnzbdService
{
    /// <summary>
    /// SABnzbd has no way to stop fetching individual files before a job finishes - the whole NZB is already pulled
    /// from Usenet by the time Cleanuparr can act. Instead, this scans a completed job's output files on disk and
    /// deletes the ones that match the blocklist, reusing the same scanning engine every other client uses.
    /// </summary>
    /// <inheritdoc/>
    public override async Task<BlockFilesResult> BlockUnwantedFilesAsync(string downloadId, IReadOnlyList<string> ignoredDownloads)
    {
        BlockFilesResult result = new();
        (SabnzbdQueueSlot? queueSlot, SabnzbdHistorySlot? historySlot) = await FindAsync(downloadId);

        if (queueSlot is null && historySlot is null)
        {
            _logger.LogDebug("Failed to find download {DownloadId} in the {Name} download client", downloadId, _downloadClientConfig.Name);
            return result;
        }

        // Still downloading, or finished but not successfully: nothing to scan yet, but it IS in this
        // client, so report Found so the caller doesn't log a false "not found in any client" warning.
        if (historySlot is not { Status: "Completed" } || string.IsNullOrEmpty(historySlot.Storage))
        {
            result.Found = true;
            result.IsPrivate = false;
            result.Item = queueSlot is not null
                ? new SabnzbdItemWrapper(queueSlot)
                : new SabnzbdItemWrapper(historySlot!);
            SetDownloadClientContext();
            _logger.LogDebug("skip | download is not a completed SABnzbd history item | {DownloadId}", downloadId);
            return result;
        }

        SabnzbdItemWrapper torrent = new(historySlot);
        result.Found = true;
        result.IsPrivate = false;
        result.Item = torrent;
        SetDownloadClientContext();

        if (ignoredDownloads.Count > 0 && torrent.IsIgnored(ignoredDownloads))
        {
            _logger.LogInformation("skip | download is ignored | {Name}", torrent.Name);
            return result;
        }

        string storagePath = RemapAndTrim(historySlot.Storage);
        string[] files;

        if (File.Exists(storagePath))
        {
            files = [storagePath];
        }
        else if (Directory.Exists(storagePath))
        {
            files = Directory.GetFiles(storagePath, "*", SearchOption.AllDirectories);
        }
        else
        {
            _logger.LogDebug("skip files check | storage not found | {Name}", torrent.Name);
            return result;
        }

        ContentBlockerConfig malwareBlockerConfig = ContextProvider.Get<ContentBlockerConfig>();

        if (files.Length is 0)
        {
            _logger.LogDebug("skip files check | no files found | {Name}", torrent.Name);
            return result;
        }

        await ApplyFileBlockingAsync(result, torrent.Name, BuildScanItems(files), malwareBlockerConfig.DeleteIfAnyFileBlocked, unwantedIndices => DeleteUnwantedFiles(files, unwantedIndices));

        return result;
    }

    private static IEnumerable<(int Index, string ValidationName, string LogName, FileBlockAction Action)> BuildScanItems(string[] files)
    {
        for (int i = 0; i < files.Length; i++)
        {
            yield return (i, Path.GetFileName(files[i]), files[i], FileBlockAction.CheckBlocklist);
        }
    }

    private Task DeleteUnwantedFiles(string[] files, List<int> unwantedIndices)
    {
        foreach (int index in unwantedIndices)
        {
            TryDeleteFiles(files[index], failOnNotFound: false);
        }

        return Task.CompletedTask;
    }
}
