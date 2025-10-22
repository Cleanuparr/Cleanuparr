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
        
        result.IsPrivate = download.Private;
        result.Found = true;

        // Create ITorrentItem wrapper for consistent interface usage
        var torrentItem = new DelugeItem(download);

        if (torrentItem.IsIgnored(ignoredDownloads))
        {
            _logger.LogInformation("skip | download is ignored | {name}", torrentItem.Name);
            return result;
        }

        try
        {
            contents = await _client.GetTorrentFiles(hash);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "failed to find files in the download client | {name}", torrentItem.Name);
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
            _logger.LogTrace("all files are unwanted | removing download | {name}", torrentItem.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesSkipped;
            result.DeleteFromClient = true;
            return result;
        }
        
        // remove if download is stuck
        (result.ShouldRemove, result.DeleteReason, result.DeleteFromClient) = await EvaluateDownloadRemoval(torrentItem);

        return result;
    }

    private async Task<(bool, DeleteReason, bool)> EvaluateDownloadRemoval(ITorrentItem torrentItem)
    {
        (bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient) result = await CheckIfSlow(torrentItem);

        if (result.ShouldRemove)
        {
            return result;
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
        if (!torrentItem.IsStalled())
        {
            _logger.LogTrace("skip stalled check | download is not in stalled state | {name}", torrentItem.Name);
            return (false, DeleteReason.None, false);
        }

        return await _ruleEvaluator.EvaluateStallRulesAsync(torrentItem);
    }
}