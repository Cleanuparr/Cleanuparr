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
    public override async Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        BlockFilesResult result = new();
        (SabnzbdQueueSlot? queueSlot, SabnzbdHistorySlot? historySlot, double serverKbPerSec, bool queuePaused) = await FindAsync(hash);

        if (queueSlot is null && historySlot is null)
        {
            _logger.LogDebug("Failed to find download {Hash} in the {Name} download client", hash, _downloadClientConfig.Name);
            return result;
        }

        // Still downloading, or finished but not successfully: nothing to scan yet, but it IS in this
        // client, so report Found so the caller doesn't log a false "not found in any client" warning.
        if (historySlot is not { Status: "Completed" } || string.IsNullOrEmpty(historySlot.Storage))
        {
            result.Found = true;
            result.IsPrivate = false;
            result.Torrent = queueSlot is not null
                ? new SabnzbdItemWrapper(queueSlot, serverKbPerSec, queuePaused)
                : new SabnzbdItemWrapper(historySlot!);
            SetDownloadClientContext();
            _logger.LogDebug("skip | download is not a completed SABnzbd history item | {Hash}", hash);
            return result;
        }

        SabnzbdItemWrapper torrent = new(historySlot);
        result.Found = true;
        result.IsPrivate = false;
        result.Torrent = torrent;
        SetDownloadClientContext();

        if (ignoredDownloads.Count > 0 && torrent.IsIgnored(ignoredDownloads))
        {
            _logger.LogInformation("skip | download is ignored | {Name}", torrent.Name);
            return result;
        }

        if (!Directory.Exists(historySlot.Storage))
        {
            _logger.LogDebug("skip files check | storage directory not found | {Name}", torrent.Name);
            return result;
        }

        var malwareBlockerConfig = ContextProvider.Get<ContentBlockerConfig>();
        string[] files = Directory.GetFiles(historySlot.Storage, "*", SearchOption.AllDirectories);

        if (files.Length is 0)
        {
            _logger.LogDebug("skip files check | no files found | {Name}", torrent.Name);
            return result;
        }

        Dictionary<int, string> filesByIndex = [];

        IEnumerable<(int Index, string ValidationName, string LogName, FileBlockAction Action)> BuildScanItems()
        {
            for (int i = 0; i < files.Length; i++)
            {
                filesByIndex[i] = files[i];
                yield return (i, Path.GetFileName(files[i]), files[i], FileBlockAction.CheckBlocklist);
            }
        }

        await ApplyFileBlockingAsync(result, torrent.Name, BuildScanItems(), malwareBlockerConfig.DeleteIfAnyFileBlocked, unwantedIndices =>
        {
            foreach (int index in unwantedIndices)
            {
                TryDeleteFiles(filesByIndex[index], failOnNotFound: false);
            }

            return Task.CompletedTask;
        });

        return result;
    }
}
