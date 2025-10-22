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

        IReadOnlyList<TorrentTracker> trackers = await GetTrackersAsync(hash);

        TorrentProperties? torrentProperties = await _client.GetTorrentPropertiesAsync(hash);

        if (torrentProperties is null)
        {
            _logger.LogError("Failed to find torrent properties for {name}", download.Name);
            return result;
        }

        result.IsPrivate = torrentProperties.AdditionalData.TryGetValue("is_private", out var dictValue) &&
                           bool.TryParse(dictValue?.ToString(), out bool boolValue)
                           && boolValue;
        
        result.Found = true;

        // Create ITorrentItem wrapper for consistent interface usage
        var torrentItem = new QBitItem(download, trackers, result.IsPrivate);

        if (torrentItem.IsIgnored(ignoredDownloads))
        {
            _logger.LogInformation("skip | download is ignored | {name}", torrentItem.Name);
            return result;
        }

        IReadOnlyList<TorrentContent>? files = await _client.GetTorrentContentsAsync(hash);

        if (files?.Count is > 0 && files.All(x => x.Priority is TorrentContentPriority.Skip))
        {
            result.ShouldRemove = true;

            // if all files were blocked by qBittorrent
            if (download is { CompletionOn: not null, Downloaded: null or 0 })
            {
                _logger.LogDebug("all files are unwanted by qBit | removing download | {name}", torrentItem.Name);
                result.DeleteReason = DeleteReason.AllFilesSkippedByQBit;
                result.DeleteFromClient = true;
                return result;
            }

            // remove if all files are unwanted
            _logger.LogDebug("all files are unwanted | removing download | {name}", torrentItem.Name);
            result.DeleteReason = DeleteReason.AllFilesSkipped;
            result.DeleteFromClient = true;
            return result;
        }

        (result.ShouldRemove, result.DeleteReason, result.DeleteFromClient) = await EvaluateDownloadRemoval(torrentItem);

        return result;
    }

    private async Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient)> EvaluateDownloadRemoval(ITorrentItem torrentItem)
    {
        (bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient) slowResult = await CheckIfSlow(torrentItem);

        if (slowResult.ShouldRemove)
        {
            return slowResult;
        }

        return await CheckIfStuck(torrentItem);
    }

    private async Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient)> CheckIfSlow(ITorrentItem torrentItem)
    {
        if (!torrentItem.IsDownloading())
        {
            _logger.LogTrace("skip slow check | download is not in downloading state | {name}", torrentItem.Name);
            return (false, DeleteReason.None, false);
        }

        if (torrentItem.DownloadSpeed <= 0)
        {
            _logger.LogTrace("skip slow check | download speed is 0 | {name}", torrentItem.Name);
            return (false, DeleteReason.None, false);
        }

        return await _ruleEvaluator.EvaluateSlowRulesAsync(torrentItem);
    }

    private async Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient)> CheckIfStuck(ITorrentItem torrentItem)
    {
        if (torrentItem.IsMetadataDownloading())
        {
            var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));

            if (queueCleanerConfig.DownloadingMetadataMaxStrikes > 0)
            {
                bool shouldRemove = await _striker.StrikeAndCheckLimit(
                    torrentItem.Hash,
                    torrentItem.Name,
                    queueCleanerConfig.DownloadingMetadataMaxStrikes,
                    StrikeType.DownloadingMetadata
                );
                
                return (shouldRemove, DeleteReason.DownloadingMetadata, shouldRemove);
            }

            return (false, DeleteReason.None, false);
        }

        if (!torrentItem.IsStalled())
        {
            _logger.LogTrace("skip stalled check | download is not in stalled state | {name}", torrentItem.Name);
            return (false, DeleteReason.None, false);
        }

        return await _ruleEvaluator.EvaluateStallRulesAsync(torrentItem);
    }
}