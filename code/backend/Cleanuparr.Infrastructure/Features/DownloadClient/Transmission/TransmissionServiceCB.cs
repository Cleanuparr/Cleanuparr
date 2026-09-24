using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Microsoft.Extensions.Logging;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

public partial class TransmissionService
{
    /// <inheritdoc/>
    public override async Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        TorrentInfo? download = await GetTorrentAsync(hash);
        BlockFilesResult result = new();

        if (download?.FileStats is null || download.FileStats.Length == 0)
        {
            _logger.LogDebug("failed to find torrent {hash} in the {name} download client", hash, _downloadClientConfig.Name);
            return result;
        }
        
        if (download.Files is null)
        {
            _logger.LogDebug("torrent {hash} has no files", hash);
            return result;
        }
        
        if (ignoredDownloads.Count > 0 && download.ShouldIgnore(ignoredDownloads))
        {
            _logger.LogDebug("skip | download is ignored | {name}", download.Name);
            return result;
        }

        bool isPrivate = download.IsPrivate ?? false;
        result.IsPrivate = isPrivate;
        result.Found = true;
        result.Torrent = new TransmissionItemWrapper(download);
        SetDownloadClientContext();

        var malwareBlockerConfig = ContextProvider.Get<ContentBlockerConfig>();
        
        if (malwareBlockerConfig.IgnorePrivate && isPrivate)
        {
            // ignore private trackers
            _logger.LogDebug("skip files check | download is private | {name}", download.Name);
            return result;
        }

        InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        BlocklistType blocklistType = _blocklistProvider.GetBlocklistType(instanceType);
        ConcurrentBag<string> patterns = _blocklistProvider.GetPatterns(instanceType);
        ConcurrentBag<Regex> regexes = _blocklistProvider.GetRegexes(instanceType);

        IEnumerable<(int Index, string ValidationName, string LogName, FileBlockAction Action)> BuildScanItems()
        {
            for (int i = 0; i < download.Files.Length; i++)
            {
                if (download.FileStats?[i].Wanted == null)
                {
                    _logger.LogTrace("Skipping file with no stats | {file}", download.Files[i].Name);
                    continue;
                }

                yield return (i, download.Files[i].Name, download.Files[i].Name, download.FileStats[i].Wanted!.Value
                    ? FileBlockAction.CheckBlocklist
                    : FileBlockAction.AlreadySkipped);
            }
        }

        (List<int> unwantedIndices, long totalFiles, long totalUnwantedFiles, bool deleteImmediately) =
            ScanFilesForBlocking(BuildScanItems(), blocklistType, patterns, regexes, malwareBlockerConfig.DeleteIfAnyFileBlocked);

        if (deleteImmediately)
        {
            _logger.LogDebug("at least one file is blocked for {name}", download.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AtLeastOneFileBlocked;
            return result;
        }

        if (unwantedIndices.Count is 0)
        {
            _logger.LogDebug("No unwanted files found for {name}", download.Name);
            return result;
        }

        if (totalUnwantedFiles == totalFiles)
        {
            _logger.LogDebug("All files are blocked for {name}", download.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesBlocked;
        }

        _logger.LogDebug("Marking {count} unwanted files as skipped for {name}", totalUnwantedFiles, download.Name);

        await _dryRunInterceptor.InterceptAsync(() => SetUnwantedFiles(download.Id, unwantedIndices.Select(i => (long)i).ToArray()));

        return result;
    }
}