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
        
        // Use rule-based evaluation instead of global configuration
        await EvaluateDownloadRemovalWithRules(download, result);

        return result;
    }
    
    private async Task EvaluateDownloadRemovalWithRules(DownloadStatus download, DownloadCheckResult result)
    {
        // Create ITorrentInfo wrapper for rule evaluation
        var torrentInfo = new DelugeTorrentInfo(download);
        
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