using Cleanuparr.Domain.Enums;
using Microsoft.Extensions.Logging;
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
        SetDownloadClientContext();

        // Create ITorrentItem wrapper for consistent interface usage
        TransmissionItemWrapper torrent = new(download);
        result.Torrent = torrent;

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
        (result.ShouldRemove, result.DeleteReason, result.DeleteFromClient, result.ChangeCategory) = await EvaluateDownloadRemoval(torrent);

        return result;
    }

    /// <inheritdoc/>
    protected override Func<Task<bool>>? GetAltSpeedLimitProbe() => IsAltSpeedLimitActiveAsync;
}
