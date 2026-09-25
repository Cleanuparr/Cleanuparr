using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

public partial class UTorrentService
{
    /// <inheritdoc/>
    public override async Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        hash = hash.ToLowerInvariant();

        UTorrentItem? download = await _client.GetTorrentAsync(hash);
        BlockFilesResult result = new();
        
        if (download?.Hash is null)
        {
            _logger.LogDebug("Failed to find torrent {Hash} in the download client", hash);
            return result;
        }
        
        UTorrentProperties? properties = await _client.GetTorrentPropertiesAsync(hash);

        if (properties is null)
        {
            _logger.LogDebug("Failed to find torrent {Hash} in the download client", hash);
            return result;
        }

        result.IsPrivate = properties.IsPrivate;
        result.Found = true;
        result.Torrent = new UTorrentItemWrapper(download, properties, _timeProvider);
        SetDownloadClientContext();

        if (ignoredDownloads.Count > 0 &&
            (download.ShouldIgnore(ignoredDownloads) || properties.TrackerList.Any(x => x.ShouldIgnore(ignoredDownloads))))
        {
            _logger.LogInformation("skip | download is ignored | {Name}", download.Name);
            return result;
        }

        var malwareBlockerConfig = ContextProvider.Get<ContentBlockerConfig>();
        
        if (malwareBlockerConfig.IgnorePrivate && result.IsPrivate)
        {
            // ignore private trackers
            _logger.LogDebug("skip files check | download is private | {Name}", download.Name);
            return result;
        }
        
        List<UTorrentFile>? files = await _client.GetTorrentFilesAsync(hash);

        if (files?.Count is null or 0)
        {
            _logger.LogDebug("skip files check | no files found | {Name}", download.Name);
            return result;
        }

        IEnumerable<(int Index, string ValidationName, string LogName, FileBlockAction Action)> BuildScanItems()
        {
            for (int i = 0; i < files.Count; i++)
            {
                UTorrentFile file = files[i];
                yield return (i, file.Name, file.Name, file.Priority == 0
                    ? FileBlockAction.AlreadySkipped
                    : FileBlockAction.CheckBlocklist);
            }
        }

        await ApplyFileBlockingAsync(result, download.Name, BuildScanItems(), malwareBlockerConfig.DeleteIfAnyFileBlocked, async unwantedIndices =>
        {
            await _client.SetFilesPriorityAsync(hash, unwantedIndices, 0);
        });

        return result;
    }
}