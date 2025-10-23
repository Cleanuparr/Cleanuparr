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

        var properties = await _client.GetTorrentPropertiesAsync(hash);
        result.IsPrivate = properties.IsPrivate;
        result.Found = true;

        // Create ITorrentItem wrapper for consistent interface usage
        var torrentItem = new UTorrentItemWrapper(download, properties);

        if (torrentItem.IsIgnored(ignoredDownloads))
        {
            _logger.LogInformation("skip | download is ignored | {name}", torrentItem.Name);
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
            _logger.LogDebug("all files are unwanted | removing download | {name}", torrentItem.Name);
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
