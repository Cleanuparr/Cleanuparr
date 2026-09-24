using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Microsoft.Extensions.Logging;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

public partial class QBitService
{
    /// <inheritdoc/>
    public override async Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        TorrentInfo? download = (await _client.GetTorrentListAsync(new TorrentListQuery { Hashes = [hash] }))
            ?.FirstOrDefault();
        BlockFilesResult result = new();

        if (download is null)
        {
            _logger.LogDebug("failed to find torrent {Hash} in the {Name} download client", hash, _downloadClientConfig.Name);
            return result;
        }
        
        IReadOnlyList<TorrentTracker>? trackers = await GetTrackersAsync(hash);

        if (trackers is null)
        {
            _logger.LogDebug("failed to find torrent {Hash} in the {Name} download client", hash, _downloadClientConfig.Name);
            return result;
        }

        if (ignoredDownloads.Count > 0 &&
            (download.ShouldIgnore(ignoredDownloads) || trackers.Any(x => x.ShouldIgnore(ignoredDownloads)) is true))
        {
            _logger.LogInformation("skip | download is ignored | {Name}", download.Name);
            return result;
        }
        
        TorrentProperties? torrentProperties = await _client.GetTorrentPropertiesAsync(hash);

        if (torrentProperties is null)
        {
            _logger.LogError("Failed to find torrent properties {Name}", download.Name);
            return result;
        }

        bool isPrivate = torrentProperties.AdditionalData.TryGetValue("is_private", out var dictValue) &&
                         bool.TryParse(dictValue?.ToString(), out bool boolValue)
                         && boolValue;

        result.IsPrivate = isPrivate;
        result.Found = true;
        result.Torrent = new QBitItemWrapper(download, trackers, isPrivate);
        SetDownloadClientContext();

        var malwareBlockerConfig = ContextProvider.Get<ContentBlockerConfig>();

        if (malwareBlockerConfig.IgnorePrivate && isPrivate)
        {
            // ignore private trackers
            _logger.LogDebug("skip files check | download is private | {Name}", download.Name);
            return result;
        }
        
        IReadOnlyList<TorrentContent>? files = await _client.GetTorrentContentsAsync(hash);

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
            foreach (TorrentContent file in files)
            {
                if (!file.Index.HasValue)
                {
                    _logger.LogTrace("Skipping file with no index | {File}", file.Name);
                    continue;
                }

                yield return (file.Index.Value, file.Name, file.Name, file.Priority is TorrentContentPriority.Skip
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
            _logger.LogDebug("No unwanted files found for {Name}", download.Name);
            return result;
        }

        if (totalUnwantedFiles == totalFiles)
        {
            _logger.LogDebug("All files are blocked for {Name}", download.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesBlocked;
        }

        _logger.LogDebug("Marking {Count} unwanted files as skipped for {Name}", totalUnwantedFiles, download.Name);

        foreach (int fileIndex in unwantedIndices)
        {
            await _dryRunInterceptor.InterceptAsync(() => MarkFileAsSkipped(hash, fileIndex));
        }

        return result;
    }
    
    protected virtual async Task MarkFileAsSkipped(string hash, int fileIndex)
    {
        await _client.SetFilePriorityAsync(hash, fileIndex, TorrentContentPriority.Skip);
    }
}