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
    public override async Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        DownloadCheckResult result = new();
        TorrentInfo? download = await GetTorrentAsync(hash);

        if (download is null)
        {
            _logger.LogDebug("Failed to find torrent {hash} in the {name} download client", hash, _downloadClientConfig.Name);
            return result;
        }

        bool isPrivate = download.IsPrivate ?? false;
        result.IsPrivate = isPrivate;
        result.Found = true;

        // Create ITorrentItem wrapper for consistent interface usage
        TransmissionItemWrapper torrent = new(download);

        if (torrent.IsIgnored(ignoredDownloads))
        {
            _logger.LogDebug("skip | download is ignored | {name}", torrent.Name);
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
            _logger.LogDebug("all files are unwanted | removing download | {name}", torrent.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesSkipped;
            result.DeleteFromClient = true;
            return result;
        }

        // remove if download is stuck
        (result.ShouldRemove, result.DeleteReason, result.DeleteFromClient) = await EvaluateDownloadRemoval(torrent);

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

    private async Task<(bool, DeleteReason, bool)> EvaluateDownloadRemoval(ITorrentItemWrapper wrapper)
    {
        (bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient) result = await CheckIfSlow(wrapper);

        if (result.ShouldRemove)
        {
            return result;
        }

        return await CheckIfStuck(wrapper);
    }


    private async Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient)> CheckIfSlow(ITorrentItemWrapper wrapper)
    {
        if (!wrapper.IsDownloading())
        {
            _logger.LogTrace("skip slow check | download is not in downloading state | {name}", wrapper.Name);
            return (false, DeleteReason.None, false);
        }

        if (wrapper.DownloadSpeed <= 0)
        {
            _logger.LogTrace("skip slow check | download speed is 0 | {name}", wrapper.Name);
            return (false, DeleteReason.None, false);
        }

        return await _ruleEvaluator.EvaluateSlowRulesAsync(wrapper);
    }

    private async Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient)> CheckIfStuck(ITorrentItemWrapper wrapper)
    {
        if (!wrapper.IsStalled())
        {
            _logger.LogTrace("skip stalled check | download is not in stalled state | {name}", wrapper.Name);
            return (false, DeleteReason.None, false);
        }

        return await _ruleEvaluator.EvaluateStallRulesAsync(wrapper);
    }
}
