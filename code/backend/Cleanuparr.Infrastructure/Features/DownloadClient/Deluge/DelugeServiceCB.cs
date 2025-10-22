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
            _logger.LogDebug(exception, "failed to find files in the download client | {name}", download.Name);
        }

        if (contents is null)
        {
            return result;
        }
        
        Dictionary<int, int> priorities = [];
        bool hasPriorityUpdates = false;
        long totalFiles = 0;
        long totalUnwantedFiles = 0;
        
        InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        BlocklistType blocklistType = _blocklistProvider.GetBlocklistType(instanceType);
        ConcurrentBag<string> patterns = _blocklistProvider.GetPatterns(instanceType);
        ConcurrentBag<Regex> regexes = _blocklistProvider.GetRegexes(instanceType);
        ConcurrentBag<string> malwarePatterns = _blocklistProvider.GetMalwarePatterns();

        ProcessFiles(contents.Contents, (name, file) =>
        {
            totalFiles++;
            int priority = file.Priority;

            if (result.ShouldRemove)
            {
                return;
            }
            
            if (malwareBlockerConfig.DeleteKnownMalware && _filenameEvaluator.IsKnownMalware(name, malwarePatterns))
            {
                _logger.LogInformation("malware file found | {file} | {title}", file.Path, download.Name);
                result.ShouldRemove = true;
                result.DeleteReason = DeleteReason.MalwareFileFound;
            }

            if (file.Priority is 0)
            {
                _logger.LogTrace("File is already skipped | {file}", file.Path);
                totalUnwantedFiles++;
            }

            if (file.Priority is not 0 && !_filenameEvaluator.IsValid(name, blocklistType, patterns, regexes))
            {
                totalUnwantedFiles++;
                priority = 0;
                hasPriorityUpdates = true;
                _logger.LogInformation("unwanted file found | {file}", file.Path);
            }
            
            _logger.LogTrace("File is valid | {file}", file.Path);
            priorities.Add(file.Index, priority);
        });

        if (result.ShouldRemove)
        {
            return result;
        }

        if (!hasPriorityUpdates)
        {
            return result;
        }
        
        _logger.LogDebug("changing priorities | torrent {hash}", hash);

        List<int> sortedPriorities = priorities
            .OrderBy(x => x.Key)
            .Select(x => x.Value)
            .ToList();

        if (totalUnwantedFiles == totalFiles)
        {
            _logger.LogDebug("All files are blocked for {name}", download.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesBlocked;
        }
        
        _logger.LogDebug("Marking {count} unwanted files as skipped for {name}", totalUnwantedFiles, download.Name);

        await _dryRunInterceptor.InterceptAsync(ChangeFilesPriority, hash, sortedPriorities);

        return result;
    }
    
    protected virtual async Task ChangeFilesPriority(string hash, List<int> sortedPriorities)
    {
        await _client.ChangeFilesPriority(hash, sortedPriorities);
    }
}