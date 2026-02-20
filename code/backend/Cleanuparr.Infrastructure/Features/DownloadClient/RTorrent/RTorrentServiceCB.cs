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

        // Get trackers for ignore check
        var trackers = await _client.GetTrackersAsync(hash);
        var torrentWrapper = new RTorrentItemWrapper(download, trackers);

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

        bool hasPriorityUpdates = false;
        long totalFiles = 0;
        long totalUnwantedFiles = 0;

        InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        BlocklistType blocklistType = _blocklistProvider.GetBlocklistType(instanceType);
        ConcurrentBag<string> patterns = _blocklistProvider.GetPatterns(instanceType);
        ConcurrentBag<Regex> regexes = _blocklistProvider.GetRegexes(instanceType);
        ConcurrentBag<string> malwarePatterns = _blocklistProvider.GetMalwarePatterns();

        List<(int Index, int Priority)> priorityUpdates = [];

        foreach (var file in files)
        {
            totalFiles++;
            string fileName = Path.GetFileName(file.Path);

            if (result.ShouldRemove)
            {
                continue;
            }

            if (malwareBlockerConfig.DeleteKnownMalware && _filenameEvaluator.IsKnownMalware(fileName, malwarePatterns))
            {
                _logger.LogInformation("malware file found | {file} | {title}", file.Path, download.Name);
                result.ShouldRemove = true;
                result.DeleteReason = DeleteReason.MalwareFileFound;
            }

            if (file.Priority == 0)
            {
                _logger.LogTrace("File is already skipped | {file}", file.Path);
                totalUnwantedFiles++;
                continue;
            }

            if (!_filenameEvaluator.IsValid(fileName, blocklistType, patterns, regexes))
            {
                totalUnwantedFiles++;
                hasPriorityUpdates = true;
                priorityUpdates.Add((file.Index, 0));
                _logger.LogInformation("unwanted file found | {file}", file.Path);
                continue;
            }

            _logger.LogTrace("File is valid | {file}", file.Path);
        }

        if (result.ShouldRemove)
        {
            return result;
        }

        if (!hasPriorityUpdates)
        {
            return result;
        }

        if (totalUnwantedFiles == totalFiles)
        {
            _logger.LogDebug("All files are blocked for {name}", download.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesBlocked;
        }

        _logger.LogDebug("Marking {count} unwanted files as skipped for {name}", priorityUpdates.Count, download.Name);

        foreach (var (index, priority) in priorityUpdates)
        {
            await _dryRunInterceptor.InterceptAsync(SetFilePriority, hash, index, priority);
        }

        return result;
    }

    protected virtual async Task SetFilePriority(string hash, int index, int priority)
    {
        await _client.SetFilePriorityAsync(hash, index, priority);
    }
}
