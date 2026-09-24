using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Entities.RTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;

public partial class RTorrentService
{
    /// <inheritdoc/>
    public override async Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        // rTorrent uses uppercase hashes
        hash = hash.ToUpperInvariant();

        RTorrentTorrent? download = await _client.GetTorrentAsync(hash);
        BlockFilesResult result = new();

        if (download?.Hash is null)
        {
            _logger.LogDebug("failed to find torrent {hash} in the {name} download client", hash, _downloadClientConfig.Name);
            return result;
        }

        result.IsPrivate = download.IsPrivate == 1;
        result.Found = true;
        SetDownloadClientContext();

        // Get trackers for ignore check
        var trackers = await _client.GetTrackersAsync(hash);
        var torrentWrapper = new RTorrentItemWrapper(download, trackers, _timeProvider);
        result.Torrent = torrentWrapper;

        if (ignoredDownloads.Count > 0 && torrentWrapper.IsIgnored(ignoredDownloads))
        {
            _logger.LogInformation("skip | download is ignored | {name}", download.Name);
            return result;
        }

        var malwareBlockerConfig = ContextProvider.Get<ContentBlockerConfig>();

        if (malwareBlockerConfig.IgnorePrivate && download.IsPrivate == 1)
        {
            _logger.LogDebug("skip files check | download is private | {name}", download.Name);
            return result;
        }

        List<RTorrentFile> files;

        try
        {
            files = await _client.GetTorrentFilesAsync(hash);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "failed to find files in the download client | {name}", download.Name);
            return result;
        }

        if (files.Count == 0)
        {
            return result;
        }

        InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        BlocklistType blocklistType = _blocklistProvider.GetBlocklistType(instanceType);
        ConcurrentBag<string> patterns = _blocklistProvider.GetPatterns(instanceType);
        ConcurrentBag<Regex> regexes = _blocklistProvider.GetRegexes(instanceType);

        IEnumerable<(int Index, string ValidationName, string LogName, FileBlockAction Action)> BuildScanItems()
        {
            foreach (RTorrentFile file in files)
            {
                yield return (file.Index, Path.GetFileName(file.Path), file.Path, file.Priority == 0
                    ? FileBlockAction.AlreadySkipped
                    : FileBlockAction.CheckBlocklist);
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
            return result;
        }

        if (totalUnwantedFiles == totalFiles)
        {
            _logger.LogDebug("All files are blocked for {name}", download.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesBlocked;
        }

        _logger.LogDebug("Marking {count} unwanted files as skipped for {name}", unwantedIndices.Count, download.Name);

        foreach (int index in unwantedIndices)
        {
            await _dryRunInterceptor.InterceptAsync(() => SetFilePriority(hash, index, 0));
        }

        return result;
    }

    protected virtual async Task SetFilePriority(string hash, int index, int priority)
    {
        await _client.SetFilePriorityAsync(hash, index, priority);
    }
}
