using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Services.Interfaces;
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
        // First check if the torrent is in a state where slow rules apply
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

        // Create ITorrentInfo wrapper for rule matching
        var torrentInfo = new TransmissionTorrentInfo(download);

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
            ByteSize currentSpeed = new ByteSize(download.RateDownload ?? long.MaxValue);
            if (currentSpeed.Bytes < minSpeed.Bytes)
            {
                isSlow = true;
            }
        }

        if (hasMaxTime && !isSlow)
        {
            SmartTimeSpan maxTime = SmartTimeSpan.FromHours(matchingRule.MaxTimeHours > 0 ? matchingRule.MaxTimeHours : matchingRule.MaxTime / 3600.0);
            SmartTimeSpan currentTime = SmartTimeSpan.FromSeconds(download.Eta ?? 0);
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
        bool shouldRemove = await _striker.StrikeAndCheckLimit(download.HashString!, download.Name!, (ushort)matchingRule.MaxStrikes, strikeType);
        return (shouldRemove, deleteReason);
    }

    private async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfStuck(TorrentInfo download)
    {
        // First check if the torrent is in a stalled state
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

        // Create ITorrentInfo wrapper for rule matching
        var torrentInfo = new TransmissionTorrentInfo(download);

        // Get matching stall rule
        var matchingRule = _ruleManager.GetMatchingStallRuleAsync(torrentInfo);

        if (matchingRule is null)
        {
            _logger.LogTrace("No stall rules match torrent '{name}'. No action will be taken.", download.Name);
            return (false, DeleteReason.None);
        }

        _logger.LogTrace("Applying stall rule '{ruleName}' to torrent '{name}'", matchingRule.Name, download.Name);

        // Apply strike
        bool shouldRemove = await _striker.StrikeAndCheckLimit(download.HashString!, download.Name!, (ushort)matchingRule.MaxStrikes, StrikeType.Stalled);
        return (shouldRemove, DeleteReason.Stalled);
    }
}