using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

public partial class QBitService
{
    /// <inheritdoc/>
    public override async Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        DownloadCheckResult result = new();
        TorrentInfo? download = (await _client.GetTorrentListAsync(new TorrentListQuery { Hashes = [hash] }))
            .FirstOrDefault();

        if (download is null)
        {
            _logger.LogDebug("failed to find torrent {hash} in the {name} download client", hash, _downloadClientConfig.Name);
            return result;
        }

        result.Found = true;

        IReadOnlyList<TorrentTracker> trackers = await GetTrackersAsync(hash);

        if (ignoredDownloads.Count > 0 &&
            (download.ShouldIgnore(ignoredDownloads) || trackers.Any(x => x.ShouldIgnore(ignoredDownloads))))
        {
            _logger.LogInformation("skip | download is ignored | {name}", download.Name);
            return result;
        }

        TorrentProperties? torrentProperties = await _client.GetTorrentPropertiesAsync(hash);

        if (torrentProperties is null)
        {
            _logger.LogError("Failed to find torrent properties for {name}", download.Name);
            return result;
        }

        result.IsPrivate = torrentProperties.AdditionalData.TryGetValue("is_private", out var dictValue) &&
                           bool.TryParse(dictValue?.ToString(), out bool boolValue)
                           && boolValue;

        IReadOnlyList<TorrentContent>? files = await _client.GetTorrentContentsAsync(hash);

        if (files?.Count is > 0 && files.All(x => x.Priority is TorrentContentPriority.Skip))
        {
            result.ShouldRemove = true;

            // if all files were blocked by qBittorrent
            if (download is { CompletionOn: not null, Downloaded: null or 0 })
            {
                _logger.LogDebug("all files are unwanted by qBit | removing download | {name}", download.Name);
                result.DeleteReason = DeleteReason.AllFilesSkippedByQBit;
                return result;
            }

            // remove if all files are unwanted
            _logger.LogDebug("all files are unwanted | removing download | {name}", download.Name);
            result.DeleteReason = DeleteReason.AllFilesSkipped;
            return result;
        }

        (result.ShouldRemove, result.DeleteReason) = await EvaluateDownloadRemoval(download, result.IsPrivate);

        return result;
    }
    
    private async Task<(bool, DeleteReason)> EvaluateDownloadRemoval(TorrentInfo torrent, bool isPrivate)
    {
        (bool ShouldRemove, DeleteReason Reason) result = await CheckIfSlow(torrent, isPrivate);

        if (result.ShouldRemove)
        {
            return result;
        }

        return await CheckIfStuck(torrent, isPrivate);
    }

    private async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfSlow(TorrentInfo download, bool isPrivate)
    {
        var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));
        
        if (queueCleanerConfig.Slow.MaxStrikes is 0)
        {
            _logger.LogTrace("skip slow check | max strikes is 0 | {name}", download.Name);
            return (false, DeleteReason.None);
        }

        if (download.State is not (TorrentState.Downloading or TorrentState.ForcedDownload))
        {
            _logger.LogTrace("skip slow check | download is in {state} state | {name}", download.State, download.Name);
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
        SmartTimeSpan currentTime = new SmartTimeSpan(download.EstimatedTime ?? TimeSpan.Zero);

        return await CheckIfSlow(
            download.Hash,
            download.Name,
            minSpeed,
            currentSpeed,
            maxTime,
            currentTime
        );
    }

    private async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfStuck(TorrentInfo torrent, bool isPrivate)
    {
        var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));
        
        if (queueCleanerConfig.Stalled.MaxStrikes is 0 && queueCleanerConfig.Stalled.DownloadingMetadataMaxStrikes is 0)
        {
            _logger.LogTrace("skip stalled check | max strikes is 0 | {name}", torrent.Name);
            return (false, DeleteReason.None);
        }

        if (torrent.State is not TorrentState.StalledDownload and not TorrentState.FetchingMetadata
            and not TorrentState.ForcedFetchingMetadata)
        {
            // ignore other states
            _logger.LogTrace("skip stalled check | download is in {state} state | {name}", torrent.State, torrent.Name);
            return (false, DeleteReason.None);
        }

        if (queueCleanerConfig.Stalled.MaxStrikes > 0 && torrent.State is TorrentState.StalledDownload)
        {
            if (queueCleanerConfig.Stalled.IgnorePrivate && isPrivate)
            {
                // ignore private trackers
                _logger.LogTrace("skip stalled check | download is private | {name}", torrent.Name);
            }
            else
            {
                ResetStalledStrikesOnProgress(torrent.Hash, torrent.Downloaded ?? 0);

                return (
                    await _striker.StrikeAndCheckLimit(torrent.Hash, torrent.Name, queueCleanerConfig.Stalled.MaxStrikes,
                        StrikeType.Stalled), DeleteReason.Stalled);
            }
        }

        if (queueCleanerConfig.Stalled.DownloadingMetadataMaxStrikes > 0 && torrent.State is not TorrentState.StalledDownload)
        {
            return (
                await _striker.StrikeAndCheckLimit(torrent.Hash, torrent.Name, queueCleanerConfig.Stalled.DownloadingMetadataMaxStrikes,
                    StrikeType.DownloadingMetadata), DeleteReason.DownloadingMetadata);
        }

        _logger.LogTrace("skip stalled check | download is not stalled | {name}", torrent.Name);
        return (false, DeleteReason.None);
    }
}