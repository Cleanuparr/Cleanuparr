using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

public partial class DelugeService
{
    /// <inheritdoc/>
    public override async Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string downloadId, IReadOnlyList<string> ignoredDownloads)
    {
        downloadId = downloadId.ToLowerInvariant();

        DelugeContents? contents = null;
        DownloadCheckResult result = new();

        DownloadStatus? download = await _client.GetTorrentStatus(downloadId);

        if (download?.Hash is null)
        {
            _logger.LogDebug("Failed to find torrent {DownloadId} in the {Name} download client", downloadId, _downloadClientConfig.Name);
            return result;
        }

        result.IsPrivate = download.Private;
        result.Found = true;
        SetDownloadClientContext();

        // Create ITorrentItem wrapper for consistent interface usage
        DelugeItemWrapper torrent = new(download);
        result.Item = torrent;

        if (torrent.IsIgnored(ignoredDownloads))
        {
            _logger.LogInformation("skip | download is ignored | {name}", torrent.Name);
            return result;
        }

        try
        {
            contents = await _client.GetTorrentFiles(downloadId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "failed to find files in the download client | {name}", torrent.Name);
        }
        

        bool shouldRemove = contents?.Contents?.Count > 0;
        
        ProcessFiles(contents?.Contents, (_, file) =>
        {
            if (file.Priority > 0)
            {
                shouldRemove = false;
            }
        });

        if (shouldRemove)
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