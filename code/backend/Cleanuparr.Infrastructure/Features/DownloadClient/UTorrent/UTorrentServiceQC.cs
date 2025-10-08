using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using Cleanuparr.Infrastructure.Services.Interfaces;
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
        // First check if the torrent is in a state where slow rules apply
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

        // Create ITorrentInfo wrapper for rule matching
        var properties = await _client.GetTorrentPropertiesAsync(download.Hash);
        var torrentInfo = new UTorrentTorrentInfo(download, properties);

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
            SmartTimeSpan currentTime = SmartTimeSpan.FromSeconds(download.ETA);
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

    private async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfStuck(UTorrentItem download, bool isPrivate)
    {
        // First check if the torrent is in a stalled state
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

        // Create ITorrentInfo wrapper for rule matching
        var properties = await _client.GetTorrentPropertiesAsync(download.Hash);
        var torrentInfo = new UTorrentTorrentInfo(download, properties);

        // Get matching stall rule
        var matchingRule = _ruleManager.GetMatchingStallRuleAsync(torrentInfo);

        if (matchingRule is null)
        {
            _logger.LogTrace("No stall rules match torrent '{name}'. No action will be taken.", download.Name);
            return (false, DeleteReason.None);
        }

        _logger.LogTrace("Applying stall rule '{ruleName}' to torrent '{name}'", matchingRule.Name, download.Name);

        // Apply strike
        bool shouldRemove = await _striker.StrikeAndCheckLimit(download.Hash, download.Name, (ushort)matchingRule.MaxStrikes, StrikeType.Stalled);
        return (shouldRemove, DeleteReason.Stalled);
    }
} 