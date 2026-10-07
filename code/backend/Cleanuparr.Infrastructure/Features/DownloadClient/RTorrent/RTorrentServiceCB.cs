using Cleanuparr.Domain.Entities.RTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;

public partial class RTorrentService
{
    /// <inheritdoc/>
    public override async Task<BlockFilesResult> BlockUnwantedFilesAsync(string downloadId, IReadOnlyList<string> ignoredDownloads)
    {
        // rTorrent uses uppercase hashes
        downloadId = downloadId.ToUpperInvariant();

        RTorrentTorrent? download = await _client.GetTorrentAsync(downloadId);
        BlockFilesResult result = new();

        if (download?.Hash is null)
        {
            _logger.LogDebug("failed to find torrent {DownloadId} in the {Name} download client", downloadId, _downloadClientConfig.Name);
            return result;
        }

        result.IsPrivate = download.IsPrivate == 1;
        result.Found = true;
        SetDownloadClientContext();

        // Get trackers for ignore check
        var trackers = await _client.GetTrackersAsync(downloadId);
        var torrentWrapper = new RTorrentItemWrapper(download, trackers, _timeProvider);
        result.Item = torrentWrapper;

        if (ignoredDownloads.Count > 0 && torrentWrapper.IsIgnored(ignoredDownloads))
        {
            _logger.LogInformation("skip | download is ignored | {name}", download.Name);
            return result;
        }

        var malwareBlockerConfig = ContextProvider.Get<ContentBlockerConfig>();

        if (malwareBlockerConfig.IgnorePrivate && download.IsPrivate == 1)
        {
            _logger.LogDebug("skip files check | download is private | {Name}", download.Name);
            return result;
        }

        List<RTorrentFile> files;

        try
        {
            files = await _client.GetTorrentFilesAsync(downloadId);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "failed to find files in the download client | {Name}", download.Name);
            return result;
        }

        if (files.Count == 0)
        {
            return result;
        }

        IEnumerable<(int Index, string ValidationName, string LogName, FileBlockAction Action)> BuildScanItems()
        {
            foreach (RTorrentFile file in files)
            {
                yield return (file.Index, Path.GetFileName(file.Path), file.Path, file.Priority == 0
                    ? FileBlockAction.AlreadySkipped
                    : FileBlockAction.CheckBlocklist);
            }
        }

        await ApplyFileBlockingAsync(result, download.Name, BuildScanItems(), malwareBlockerConfig.DeleteIfAnyFileBlocked, async unwantedIndices =>
        {
            foreach (int index in unwantedIndices)
            {
                await _client.SetFilePriorityAsync(downloadId, index, 0);
            }
        });

        return result;
    }
}
