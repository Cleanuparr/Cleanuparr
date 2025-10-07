using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

public partial class DelugeService
{
    /// <inheritdoc/>
    public override async Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string hash,
        IReadOnlyList<string> ignoredDownloads)
    {
        hash = hash.ToLowerInvariant();
        
        DelugeContents? contents = null;
        DownloadCheckResult result = new();

        DownloadStatus? download = await _client.GetTorrentStatus(hash);
        
        if (download?.Hash is null)
        {
            _logger.LogDebug("failed to find torrent {hash} in the {name} download client", hash, _downloadClientConfig.Name);
            return result;
        }
        
        result.Found = true;
        result.IsPrivate = download.Private;
        
        if (ignoredDownloads.Count > 0 && download.ShouldIgnore(ignoredDownloads))
        {
            _logger.LogInformation("skip | download is ignored | {name}", download.Name);
            return result;
        }

        try
        {
            contents = await _client.GetTorrentFiles(hash);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "failed to find files in the download client | {name}", download.Name);
        }
        

        bool shouldRemove = contents?.Contents?.Count > 0;
        
        ProcessFiles(contents.Contents, (_, file) =>
        {
            if (file.Priority > 0)
            {
                shouldRemove = false;
            }
        });

        if (shouldRemove)
        {
            // remove if all files are unwanted
            _logger.LogTrace("all files are unwanted | removing download | {name}", download.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesSkipped;
            return result;
        }
        
        // remove if download is stuck
        (result.ShouldRemove, result.DeleteReason) = await EvaluateDownloadRemoval(download);

        return result;
    }

    private async Task<(bool, DeleteReason)> EvaluateDownloadRemoval(DownloadStatus status)
    {
        (bool ShouldRemove, DeleteReason Reason) result = await CheckIfSlow(status);

        if (result.ShouldRemove)
        {
            return result;
        }

        return await CheckIfStuck(status);
    }
    
    private async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfSlow(DownloadStatus download)
    {
        var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));
        
        if (queueCleanerConfig.Slow.MaxStrikes is 0)
        {
            return (false, DeleteReason.None);
        }
        
        if (download.State is null || !download.State.Equals("Downloading", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogTrace("skip slow check | item is in {state} state | {name}", download.State, download.Name);
            return (false, DeleteReason.None);
        }
        
        if (download.DownloadSpeed <= 0)
        {
            _logger.LogTrace("skip slow check | download speed is 0 | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        if (queueCleanerConfig.Slow.IgnorePrivate && download.Private)
        {
            // ignore private trackers
            _logger.LogDebug("skip slow check | download is private | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        if (download.Size > (queueCleanerConfig.Slow.IgnoreAboveSizeByteSize?.Bytes ?? long.MaxValue))
        {
            _logger.LogDebug("skip slow check | download is too large | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        ByteSize minSpeed = queueCleanerConfig.Slow.MinSpeedByteSize;
        ByteSize currentSpeed = new ByteSize(download.DownloadSpeed);
        SmartTimeSpan maxTime = SmartTimeSpan.FromHours(queueCleanerConfig.Slow.MaxTime);
        SmartTimeSpan currentTime = SmartTimeSpan.FromSeconds(download.Eta);

        return await CheckIfSlow(
            download.Hash!,
            download.Name!,
            minSpeed,
            currentSpeed,
            maxTime,
            currentTime
        );
    }
    
    private async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfStuck(DownloadStatus status)
    {
        var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));
        
        if (queueCleanerConfig.Stalled.MaxStrikes is 0)
        {
            _logger.LogTrace("skip stalled check | max strikes is 0 | {name}", status.Name);
            return (false, DeleteReason.None);
        }
        
        if (queueCleanerConfig.Stalled.IgnorePrivate && status.Private)
        {
            // ignore private trackers
            _logger.LogDebug("skip stalled check | download is private | {name}", status.Name);
            return (false, DeleteReason.None);
        }
        
        if (status.State is null || !status.State.Equals("Downloading", StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogTrace("skip stalled check | download is in {state} state | {name}", status.State, status.Name);
            return (false, DeleteReason.None);
        }

        if (status.Eta > 0)
        {
            _logger.LogTrace("skip stalled check | download is not stalled | {name}", status.Name);
            return (false, DeleteReason.None);
        }
        
        ResetStalledStrikesOnProgress(status.Hash!, status.TotalDone);
        
        return (await _striker.StrikeAndCheckLimit(status.Hash!, status.Name!, queueCleanerConfig.Stalled.MaxStrikes, StrikeType.Stalled), DeleteReason.Stalled);
    }
}