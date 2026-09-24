using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

public partial class DelugeService
{
    /// <inheritdoc/>
    public override async Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        hash = hash.ToLowerInvariant();

        DownloadStatus? download = await _client.GetTorrentStatus(hash);
        BlockFilesResult result = new();
        
        if (download?.Hash is null)
        {
            _logger.LogDebug("failed to find torrent {hash} in the {name} download client", hash, _downloadClientConfig.Name);
            return result;
        }
        
        result.IsPrivate = download.Private;
        result.Found = true;
        result.Torrent = new DelugeItemWrapper(download);
        SetDownloadClientContext();

        if (ignoredDownloads.Count > 0 && download.ShouldIgnore(ignoredDownloads))
        {
            _logger.LogInformation("skip | download is ignored | {name}", download.Name);
            return result;
        }
        
        var malwareBlockerConfig = ContextProvider.Get<ContentBlockerConfig>();
        
        if (malwareBlockerConfig.IgnorePrivate && download.Private)
        {
            // ignore private trackers
            _logger.LogDebug("skip files check | download is private | {name}", download.Name);
            return result;
        }
        
        DelugeContents? contents = null;

        try
        {
            contents = await _client.GetTorrentFiles(hash);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "failed to find files in the download client | {name}", download.Name);
        }

        if (contents is null)
        {
            return result;
        }

        InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        BlocklistType blocklistType = _blocklistProvider.GetBlocklistType(instanceType);
        ConcurrentBag<string> patterns = _blocklistProvider.GetPatterns(instanceType);
        ConcurrentBag<Regex> regexes = _blocklistProvider.GetRegexes(instanceType);

        // Deluge's priority API takes the full per-file vector, so keep every original priority
        Dictionary<int, int> originalPriorities = [];
        List<(int Index, string ValidationName, string LogName, FileBlockAction Action)> scanItems = [];

        ProcessFiles(contents.Contents, (name, file) =>
        {
            originalPriorities[file.Index] = file.Priority;
            scanItems.Add((file.Index, name, file.Path, file.Priority is 0
                ? FileBlockAction.AlreadySkipped
                : FileBlockAction.CheckBlocklist));
        });

        (List<int> unwantedIndices, long totalFiles, long totalUnwantedFiles, bool deleteImmediately) =
            ScanFilesForBlocking(scanItems, blocklistType, patterns, regexes, malwareBlockerConfig.DeleteIfAnyFileBlocked);

        if (deleteImmediately)
        {
            _logger.LogDebug("at least one file is blocked for {name}", download.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AtLeastOneFileBlocked;
            return result;
        }

        if (unwantedIndices.Count is 0)
        {
            return result;
        }

        if (totalUnwantedFiles == totalFiles)
        {
            _logger.LogDebug("All files are blocked for {name}", download.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesBlocked;
        }

        _logger.LogDebug("changing priorities | torrent {hash}", hash);
        _logger.LogDebug("Marking {count} unwanted files as skipped for {name}", totalUnwantedFiles, download.Name);

        HashSet<int> unwantedLookup = [..unwantedIndices];
        List<int> sortedPriorities = originalPriorities
            .OrderBy(x => x.Key)
            .Select(x => unwantedLookup.Contains(x.Key) ? 0 : x.Value)
            .ToList();

        await _dryRunInterceptor.InterceptAsync(() => ChangeFilesPriority(hash, sortedPriorities));

        return result;
    }
    
    protected virtual async Task ChangeFilesPriority(string hash, List<int> sortedPriorities)
    {
        await _client.ChangeFilesPriority(hash, sortedPriorities);
    }
}