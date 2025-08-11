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

        // Use rule-based evaluation instead of global configuration
        await EvaluateDownloadRemovalWithRules(download, result);

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
    
    private async Task EvaluateDownloadRemovalWithRules(TorrentInfo download, DownloadCheckResult result)
    {
        // Create ITorrentInfo wrapper for rule evaluation
        var torrentInfo = new TransmissionTorrentInfo(download);
        
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