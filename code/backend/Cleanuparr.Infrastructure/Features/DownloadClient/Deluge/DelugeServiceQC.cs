using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Services.Interfaces;
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
        // First check if the torrent is in a state where slow rules apply
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

        // Create ITorrentInfo wrapper for rule matching
        var torrentInfo = new DelugeTorrentInfo(download);

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
            SmartTimeSpan currentTime = SmartTimeSpan.FromSeconds(download.Eta);
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
        bool shouldRemove = await _striker.StrikeAndCheckLimit(download.Hash!, download.Name!, (ushort)matchingRule.MaxStrikes, strikeType);
        return (shouldRemove, deleteReason);
    }

    private async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfStuck(DownloadStatus status)
    {
        // First check if the torrent is in a stalled state
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

        // Create ITorrentInfo wrapper for rule matching
        var torrentInfo = new DelugeTorrentInfo(status);

        // Get matching stall rule
        var matchingRule = _ruleManager.GetMatchingStallRuleAsync(torrentInfo);

        if (matchingRule is null)
        {
            _logger.LogTrace("No stall rules match torrent '{name}'. No action will be taken.", status.Name);
            return (false, DeleteReason.None);
        }

        _logger.LogTrace("Applying stall rule '{ruleName}' to torrent '{name}'", matchingRule.Name, status.Name);

        // Apply strike
        bool shouldRemove = await _striker.StrikeAndCheckLimit(status.Hash!, status.Name!, (ushort)matchingRule.MaxStrikes, StrikeType.Stalled);
        return (shouldRemove, DeleteReason.Stalled);
    }
}