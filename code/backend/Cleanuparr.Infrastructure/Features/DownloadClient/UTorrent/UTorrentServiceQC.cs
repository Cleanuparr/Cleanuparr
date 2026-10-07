using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

public partial class UTorrentService
{
    /// <inheritdoc/>
    public override async Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string downloadId, IReadOnlyList<string> ignoredDownloads)
    {
        List<UTorrentFile>? files = null;
        DownloadCheckResult result = new();

        UTorrentItem? download = await _client.GetTorrentAsync(downloadId);

        if (download?.Hash is null)
        {
            _logger.LogDebug("Failed to find torrent {DownloadId} in the {Name} download client", downloadId, _downloadClientConfig.Name);
            return result;
        }

        UTorrentProperties? properties = await _client.GetTorrentPropertiesAsync(downloadId);

        if (properties is null)
        {
            _logger.LogDebug("Failed to find torrent {DownloadId} in the {Name} download client", downloadId, _downloadClientConfig.Name);
            return result;
        }

        result.IsPrivate = properties.IsPrivate;
        result.Found = true;
        SetDownloadClientContext();

        // Create ITorrentItem wrapper for consistent interface usage
        UTorrentItemWrapper torrent = new(download, properties, _timeProvider);
        result.Item = torrent;

        if (torrent.IsIgnored(ignoredDownloads))
        {
            _logger.LogInformation("skip | download is ignored | {Name}", torrent.Name);
            return result;
        }

        try
        {
            files = await _client.GetTorrentFilesAsync(downloadId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to get files for torrent {DownloadId} in the download client", downloadId);
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
            _logger.LogDebug("all files are unwanted | removing download | {Name}", torrent.Name);
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
