using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.RTorrent.Response;
using Cleanuparr.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;

public partial class RTorrentService
{
    /// <inheritdoc/>
    public override async Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        // rTorrent uses uppercase hashes
        hash = hash.ToUpperInvariant();

        DownloadCheckResult result = new();

        RTorrentTorrent? download = await _client.GetTorrentAsync(hash);

        if (string.IsNullOrEmpty(download?.Hash))
        {
            _logger.LogDebug("Failed to find torrent {hash} in the {name} download client", hash, _downloadClientConfig.Name);
            return result;
        }

        result.IsPrivate = download.IsPrivate == 1;
        result.Found = true;
        SetDownloadClientContext();

        // Get trackers for ignore check
        var trackers = await _client.GetTrackersAsync(hash);
        RTorrentItemWrapper torrent = new(download, trackers, _timeProvider);
        result.Torrent = torrent;

        if (torrent.IsIgnored(ignoredDownloads))
        {
            _logger.LogInformation("skip | download is ignored | {name}", torrent.Name);
            return result;
        }

        List<RTorrentFile> files;
        try
        {
            files = await _client.GetTorrentFilesAsync(hash);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "failed to find files in the download client | {name}", torrent.Name);
            return result;
        }

        // Check if all files are skipped (priority = 0)
        bool hasActiveFiles = files.Any(f => f.Priority > 0);

        if (files.Count > 0 && !hasActiveFiles)
        {
            // remove if all files are unwanted
            _logger.LogTrace("all files are unwanted | removing download | {name}", torrent.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesSkipped;
            result.DeleteFromClient = true;
            return result;
        }

        // remove if download is stuck
        (result.ShouldRemove, result.DeleteReason, result.DeleteFromClient, result.ChangeCategory) = await EvaluateDownloadRemoval(torrent);

        return result;
    }
}
