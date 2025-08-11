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
        
        // Use rule-based evaluation instead of global configuration
        await EvaluateDownloadRemovalWithRules(download, properties, result);

        return result;
    }
    
    private async Task EvaluateDownloadRemovalWithRules(UTorrentItem download, UTorrentProperties properties, DownloadCheckResult result)
    {
        // Create ITorrentInfo wrapper for rule evaluation
        var torrentInfo = new UTorrentTorrentInfo(download, properties);
        
        // Evaluate stall rules first
        var stallResult = await _ruleEvaluator.EvaluateStallRulesAsync(torrentInfo);
        if (stallResult.ShouldRemove)
        {
            result.ShouldRemove = true;
            result.DeleteReason = stallResult.DeleteReason;
            return;
        }
        
        // If not stalled, evaluate slow rules
        var slowResult = await _ruleEvaluator.EvaluateSlowRulesAsync(torrentInfo);
        if (slowResult.ShouldRemove)
        {
            result.ShouldRemove = true;
            result.DeleteReason = slowResult.DeleteReason;
        }
    }
} 