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

        (result.ShouldRemove, result.DeleteReason) = await EvaluateDownloadRemoval(download, trackers, result);

        return result;
    }
    
    private async Task<(bool ShouldRemove, DeleteReason Reason)> EvaluateDownloadRemoval(TorrentInfo torrent, IReadOnlyList<TorrentTracker> trackers, DownloadCheckResult result)
    {
        (bool ShouldRemove, DeleteReason Reason) slowResult = await CheckIfSlow(torrent, trackers, result.IsPrivate);

        if (slowResult.ShouldRemove)
        {
            return slowResult;
        }

        return await CheckIfStuck(torrent, trackers, result.IsPrivate);
    }

    private async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfSlow(TorrentInfo download, IReadOnlyList<TorrentTracker> trackers, bool isPrivate)
    {
        // First check if the torrent is in a state where slow rules apply
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

        // Create ITorrentInfo wrapper for rule matching
        var torrentInfo = new QBitTorrentInfo(download, trackers, isPrivate);

        // Get matching slow rule
        var matchingRule = _ruleManager.GetMatchingSlowRuleAsync(torrentInfo);

        if (matchingRule is null)
        {
            _logger.LogTrace("No slow rules match torrent '{name}'. No action will be taken.", download.Name);
            return (false, DeleteReason.None);
        }

        _logger.LogTrace("Applying slow rule '{ruleName}' to torrent '{name}'", matchingRule.Name, download.Name);

        // Determine the strike type based on rule configuration
        bool hasMinSpeed = !string.IsNullOrWhiteSpace(matchingRule.MinSpeed);
        bool hasMaxTime = matchingRule.MaxTimeHours > 0 || matchingRule.MaxTime > 0;
        
        // TODO why do we determine this here?
        // TODO maybe separate max time from max speed?
        StrikeType strikeType = (hasMinSpeed && !hasMaxTime) ? StrikeType.SlowSpeed :
                                (!hasMinSpeed && hasMaxTime) ? StrikeType.SlowTime :
                                StrikeType.SlowSpeed; // default to speed when both configured

        DeleteReason deleteReason = strikeType == StrikeType.SlowSpeed ? DeleteReason.SlowSpeed : DeleteReason.SlowTime;

        // Check if torrent is actually slow based on thresholds
        bool isSlow = false;

        if (hasMinSpeed)
        {
            ByteSize minSpeed = matchingRule.MinSpeedByteSize;
            ByteSize currentSpeed = new ByteSize(download.DownloadSpeed);
            if (currentSpeed.Bytes < minSpeed.Bytes)
            {
                isSlow = true;
            }
        }

        if (hasMaxTime && !isSlow)
        {
            SmartTimeSpan maxTime = SmartTimeSpan.FromHours(matchingRule.MaxTimeHours > 0 ? matchingRule.MaxTimeHours : matchingRule.MaxTime / 3600.0);
            SmartTimeSpan currentTime = new SmartTimeSpan(download.EstimatedTime ?? TimeSpan.Zero);
            if (currentTime.Time.TotalSeconds > maxTime.Time.TotalSeconds && maxTime.Time.TotalSeconds > 0)
            {
                isSlow = true;
            }
        }

        if (!isSlow)
        {
            _logger.LogTrace("Torrent '{name}' doesn't violate slow thresholds", download.Name);
            return (false, DeleteReason.None);
        }

        // Apply strike
        bool shouldRemove = await _striker.StrikeAndCheckLimit(download.Hash, download.Name, (ushort)matchingRule.MaxStrikes, strikeType);
        return (shouldRemove, deleteReason);
    }

    private async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfStuck(TorrentInfo torrent, IReadOnlyList<TorrentTracker> trackers, bool isPrivate)
    {
        // Check for metadata downloading state separately
        if (torrent.State is TorrentState.FetchingMetadata or TorrentState.ForcedFetchingMetadata)
        {
            var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));

            if (queueCleanerConfig.DownloadingMetadataMaxStrikes > 0)
            {
                return (
                    await _striker.StrikeAndCheckLimit(torrent.Hash, torrent.Name, queueCleanerConfig.DownloadingMetadataMaxStrikes,
                        StrikeType.DownloadingMetadata), DeleteReason.DownloadingMetadata);
            }

            return (false, DeleteReason.None);
        }

        // First check if the torrent is in a stalled state
        if (torrent.State is not TorrentState.StalledDownload)
        {
            _logger.LogTrace("skip stalled check | download is in {state} state | {name}", torrent.State, torrent.Name);
            return (false, DeleteReason.None);
        }

        // Create ITorrentInfo wrapper for rule matching
        var torrentInfo = new QBitTorrentInfo(torrent, trackers, isPrivate);

        // Get matching stall rule
        var matchingRule = _ruleManager.GetMatchingStallRuleAsync(torrentInfo);

        if (matchingRule is null)
        {
            _logger.LogTrace("No stall rules match torrent '{name}'. No action will be taken.", torrent.Name);
            return (false, DeleteReason.None);
        }

        _logger.LogTrace("Applying stall rule '{ruleName}' to torrent '{name}'", matchingRule.Name, torrent.Name);

        // Apply strike
        bool shouldRemove = await _striker.StrikeAndCheckLimit(torrent.Hash, torrent.Name, (ushort)matchingRule.MaxStrikes, StrikeType.Stalled);
        return (shouldRemove, DeleteReason.Stalled);
    }
}