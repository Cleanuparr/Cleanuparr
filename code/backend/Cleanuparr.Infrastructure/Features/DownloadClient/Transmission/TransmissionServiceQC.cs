using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;
using Transmission.API.RPC.Arguments;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

public partial class TransmissionService
{
    /// <inheritdoc/>
    public override async Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string hash,
        IReadOnlyList<string> ignoredDownloads)
    {
        DownloadCheckResult result = new();
        TorrentInfo? download = await GetTorrentAsync(hash);

        if (download is null)
        {
            _logger.LogDebug("failed to find torrent {hash} in the {name} download client", hash, _downloadClientConfig.Name);
            return result;
        }
        
        result.Found = true;
        
        if (ignoredDownloads.Count > 0 && download.ShouldIgnore(ignoredDownloads))
        {
            _logger.LogDebug("skip | download is ignored | {name}", download.Name);
            return result;
        }
        
        bool shouldRemove = download.FileStats?.Length > 0;
        bool isPrivate = download.IsPrivate ?? false;
        result.IsPrivate = isPrivate;

        foreach (TransmissionTorrentFileStats stats in download.FileStats ?? [])
        {
            if (!stats.Wanted.HasValue)
            {
                // if any files stats are missing, do not remove
                shouldRemove = false;
            }
            
            if (stats.Wanted.HasValue && stats.Wanted.Value)
            {
                // if any files are wanted, do not remove
                shouldRemove = false;
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
        (result.ShouldRemove, result.DeleteReason) = await EvaluateDownloadRemoval(download, isPrivate);

        return result;
    }
    
    protected virtual async Task SetUnwantedFiles(long downloadId, long[] unwantedFiles)
    {
        await _client.TorrentSetAsync(new TorrentSettings
        {
            Ids = [downloadId],
            FilesUnwanted = unwantedFiles,
        });
    }
    
    private async Task<(bool, DeleteReason)> EvaluateDownloadRemoval(TorrentInfo download, bool isPrivate)
    {
        (bool ShouldRemove, DeleteReason Reason) result = await CheckIfSlow(download);

        if (result.ShouldRemove)
        {
            return result;
        }

        return await CheckIfStuck(download);
    }

    private async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfSlow(TorrentInfo download)
    {
        var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));
        
        if (queueCleanerConfig.Slow.MaxStrikes is 0)
        {
            return (false, DeleteReason.None);
        }
        
        if (download.Status is not 4)
        {
            // not in downloading state
            _logger.LogTrace("skip slow check | download is in {state} state | {name}", download.Status, download.Name);
            return (false, DeleteReason.None);
        }
        
        if (download.RateDownload <= 0)
        {
            _logger.LogTrace("skip slow check | download speed is 0 | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        if (queueCleanerConfig.Slow.IgnorePrivate && download.IsPrivate is true)
        {
            // ignore private trackers
            _logger.LogDebug("skip slow check | download is private | {name}", download.Name);
            return (false, DeleteReason.None);
        }

        if (download.TotalSize > (queueCleanerConfig.Slow.IgnoreAboveSizeByteSize?.Bytes ?? long.MaxValue))
        {
            _logger.LogDebug("skip slow check | download is too large | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        ByteSize minSpeed = queueCleanerConfig.Slow.MinSpeedByteSize;
        ByteSize currentSpeed = new ByteSize(download.RateDownload ?? long.MaxValue);
        SmartTimeSpan maxTime = SmartTimeSpan.FromHours(queueCleanerConfig.Slow.MaxTime);
        SmartTimeSpan currentTime = SmartTimeSpan.FromSeconds(download.Eta ?? 0);

        return await CheckIfSlow(
            download.HashString!,
            download.Name!,
            minSpeed,
            currentSpeed,
            maxTime,
            currentTime
        );
    }

    private async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfStuck(TorrentInfo download)
    {
        var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));
        
        if (queueCleanerConfig.Stalled.MaxStrikes is 0)
        {
            _logger.LogTrace("skip stalled check | max strikes is 0 | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        if (download.Status is not 4)
        {
            // not in downloading state
            _logger.LogTrace("skip stalled check | download is in {state} state | {name}", download.Status, download.Name);
            return (false, DeleteReason.None);
        }
        
        if (download.RateDownload > 0 || download.Eta > 0)
        {
            _logger.LogTrace("skip stalled check | download is not stalled | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        if (queueCleanerConfig.Stalled.IgnorePrivate && (download.IsPrivate ?? false))
        {
            // ignore private trackers
            _logger.LogDebug("skip stalled check | download is private | {name}", download.Name);
            return (false, DeleteReason.None);
        }
        
        ResetStalledStrikesOnProgress(download.HashString!, download.DownloadedEver ?? 0);
        
        return (await _striker.StrikeAndCheckLimit(download.HashString!, download.Name!, queueCleanerConfig.Stalled.MaxStrikes, StrikeType.Stalled), DeleteReason.Stalled);
    }
}