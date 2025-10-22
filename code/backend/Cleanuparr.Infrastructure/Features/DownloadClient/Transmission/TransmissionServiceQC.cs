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

        bool isPrivate = download.IsPrivate ?? false;
        result.IsPrivate = isPrivate;
        result.Found = true;

        // Create ITorrentItem wrapper for consistent interface usage
        var torrentItem = new TransmissionItem(download);

        if (torrentItem.IsIgnored(ignoredDownloads))
        {
            _logger.LogDebug("skip | download is ignored | {name}", torrentItem.Name);
            return result;
        }

        bool shouldRemove = download.FileStats?.Length > 0;

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

    protected virtual async Task SetUnwantedFiles(long downloadId, long[] unwantedFiles)
    {
        await _client.TorrentSetAsync(new TorrentSettings
        {
            Ids = [downloadId],
            FilesUnwanted = unwantedFiles,
        });
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
