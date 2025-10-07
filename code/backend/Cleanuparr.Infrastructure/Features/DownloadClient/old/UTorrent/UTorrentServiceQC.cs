using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

public partial class UTorrentService
{
    /// <inheritdoc/>
    public override async Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        List<UTorrentFile>? files = null;
        DownloadCheckResult result = new();

        UTorrentItem? download = await _client.GetTorrentAsync(hash);
        
        if (download?.Hash is null)
        {
            _logger.LogDebug("Failed to find torrent {hash} in the download client", hash);
            return result;
        }
        
        result.Found = true;
        
        var properties = await _client.GetTorrentPropertiesAsync(hash);
        result.IsPrivate = properties.IsPrivate;
        
        if (ignoredDownloads.Count > 0 &&
            (download.ShouldIgnore(ignoredDownloads) || properties.TrackerList.Any(x => x.ShouldIgnore(ignoredDownloads))))
        {
            _logger.LogInformation("skip | download is ignored | {name}", download.Name);
            return result;
        }

        try
        {
            files = await _client.GetTorrentFilesAsync(hash);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Failed to get files for torrent {hash} in the download client", hash);
        }

        bool shouldRemove = files?.Count > 0;
        
        foreach (var file in files ?? [])
        {
            if (file.Priority > 0) // 0 = skip, >0 = wanted
            {
                shouldRemove = false;
                break;
            }
        }

        if (shouldRemove)
        {
            // remove if all files are unwanted
            _logger.LogDebug("all files are unwanted | removing download | {name}", download.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesSkipped;
            return result;
        }
        
        // remove if download is stuck
        (result.ShouldRemove, result.DeleteReason) = await EvaluateDownloadRemoval(download, result.IsPrivate);

        return result;
    }

    private async Task<(bool, DeleteReason)> EvaluateDownloadRemoval(UTorrentItem torrent, bool isPrivate)
    {
        (bool ShouldRemove, DeleteReason Reason) result = await CheckIfSlow(torrent, isPrivate);

        if (result.ShouldRemove)
        {
            return result;
        }

        return await CheckIfStuck(torrent, isPrivate);
    }
    
    private async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfSlow(UTorrentItem download, bool isPrivate)
    {
        var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));
        
        if (queueCleanerConfig.Slow.MaxStrikes is 0)
        {
            _logger.LogTrace("skip slow check | max strikes is 0 | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        if (!download.IsDownloading())
        {
            _logger.LogTrace("skip slow check | download is in {state} state | {name}", download.StatusMessage, download.Name);
            return (false, DeleteReason.None);
        }
        
        if (download.DownloadSpeed <= 0)
        {
            _logger.LogTrace("skip slow check | download speed is 0 | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        if (queueCleanerConfig.Slow.IgnorePrivate && isPrivate)
        {
            // ignore private trackers
            _logger.LogTrace("skip slow check | download is private | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        if (download.Size > (queueCleanerConfig.Slow.IgnoreAboveSizeByteSize?.Bytes ?? long.MaxValue))
        {
            _logger.LogTrace("skip slow check | download is too large | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        ByteSize minSpeed = queueCleanerConfig.Slow.MinSpeedByteSize;
        ByteSize currentSpeed = new ByteSize(download.DownloadSpeed);
        SmartTimeSpan maxTime = SmartTimeSpan.FromHours(queueCleanerConfig.Slow.MaxTime);
        SmartTimeSpan currentTime = SmartTimeSpan.FromSeconds(download.ETA);

        return await CheckIfSlow(
            download.Hash,
            download.Name,
            minSpeed,
            currentSpeed,
            maxTime,
            currentTime
        );
    }
    
    private async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfStuck(UTorrentItem download, bool isPrivate)
    {
        var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));
        
        if (queueCleanerConfig.Stalled.MaxStrikes is 0)
        {
            _logger.LogTrace("skip stalled check | max strikes is 0 | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        if (queueCleanerConfig.Stalled.IgnorePrivate && isPrivate)
        {
            _logger.LogDebug("skip stalled check | download is private | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        if (!download.IsDownloading())
        {
            _logger.LogTrace("skip stalled check | download is in {state} state | {name}", download.StatusMessage, download.Name);
            return (false, DeleteReason.None);
        }

        if (download.DateCompleted > 0)
        {
            _logger.LogTrace("skip stalled check | download is completed | {name}", download.Name);
            return (false, DeleteReason.None);
        }

        if (download.DownloadSpeed > 0 || download.ETA > 0)
        {
            _logger.LogTrace("skip stalled check | download is not stalled | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        ResetStalledStrikesOnProgress(download.Hash, download.Downloaded);
        
        return (await _striker.StrikeAndCheckLimit(download.Hash, download.Name, queueCleanerConfig.Stalled.MaxStrikes, StrikeType.Stalled), DeleteReason.Stalled);
    }
} 