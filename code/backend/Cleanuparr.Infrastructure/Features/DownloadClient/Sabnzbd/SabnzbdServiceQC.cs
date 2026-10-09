using Cleanuparr.Domain.Entities.Sabnzbd;
using Cleanuparr.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;

public partial class SabnzbdService
{
    /// <inheritdoc/>
    public override async Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string downloadId, IReadOnlyList<string> ignoredDownloads)
    {
        DownloadCheckResult result = new();
        (SabnzbdQueueSlot? queueSlot, SabnzbdHistorySlot? historySlot) = await FindAsync(downloadId);

        if (queueSlot is null && historySlot is null)
        {
            _logger.LogDebug("Failed to find download {DownloadId} in the {Name} download client", downloadId, _downloadClientConfig.Name);
            return result;
        }

        result.Found = true;
        result.IsPrivate = false;
        SetDownloadClientContext();

        SabnzbdItemWrapper torrent = queueSlot is not null
            ? new SabnzbdItemWrapper(queueSlot)
            : new SabnzbdItemWrapper(historySlot!);
        result.Item = torrent;

        if (torrent.IsIgnored(ignoredDownloads))
        {
            _logger.LogDebug("skip | download is ignored | {Name}", torrent.Name);
            return result;
        }

        // SABnzbd fails a job that stops progressing on its own; trust that signal rather than guessing from live throughput.
        if (historySlot is { Status: "Failed" })
        {
            _logger.LogInformation("download failed in SABnzbd history | {Reason} | {Name}", historySlot.FailMessage, torrent.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.DownloadFailed;
            result.DeleteFromClient = true;
        }

        return result;
    }

    /// <summary>
    /// Looks up a job by its SABnzbd <c>nzo_id</c>, checking the active queue first, then history.
    /// </summary>
    private async Task<(SabnzbdQueueSlot? Queue, SabnzbdHistorySlot? History)> FindAsync(string nzoId)
    {
        SabnzbdQueueData? queue = await _client.GetQueueAsync(nzoId);
        SabnzbdQueueSlot? queueSlot = queue?.Slots.FirstOrDefault(s => string.Equals(s.NzoId, nzoId, StringComparison.OrdinalIgnoreCase));

        if (queueSlot is not null)
        {
            return (queueSlot, null);
        }

        SabnzbdHistoryData? history = await _client.GetHistoryAsync(nzoId);
        SabnzbdHistorySlot? historySlot = history?.Slots.FirstOrDefault(s => string.Equals(s.NzoId, nzoId, StringComparison.OrdinalIgnoreCase));

        return (null, historySlot);
    }
}
