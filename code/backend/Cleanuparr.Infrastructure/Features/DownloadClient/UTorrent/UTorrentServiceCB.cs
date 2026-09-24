using System.Collections.Concurrent;
using System.Text.RegularExpressions;
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

        InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        BlocklistType blocklistType = _blocklistProvider.GetBlocklistType(instanceType);
        ConcurrentBag<string> patterns = _blocklistProvider.GetPatterns(instanceType);
        ConcurrentBag<Regex> regexes = _blocklistProvider.GetRegexes(instanceType);

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

        (List<int> unwantedIndices, long totalFiles, long totalUnwantedFiles, bool deleteImmediately) =
            ScanFilesForBlocking(BuildScanItems(), blocklistType, patterns, regexes, malwareBlockerConfig.DeleteIfAnyFileBlocked);

        if (deleteImmediately)
        {
            _logger.LogDebug("at least one file is blocked for {Name}", download.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AtLeastOneFileBlocked;
            return result;
        }

        if (unwantedIndices.Count is 0)
        {
            return result;
        }

        _logger.LogDebug("changing priorities | torrent {Hash}", hash);

        if (totalUnwantedFiles == totalFiles)
        {
            _logger.LogDebug("All files are blocked for {Name}", download.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesBlocked;
        }

        await _dryRunInterceptor.InterceptAsync(() => ChangeFilesPriority(hash, unwantedIndices));

        return result;
    }
    
    protected virtual async Task ChangeFilesPriority(string hash, List<int> fileIndexes)
    {
        await _client.SetFilesPriorityAsync(hash, fileIndexes, 0);
    }
} 