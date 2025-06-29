using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Entities.UTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using Cleanuparr.Persistence.Models.Configuration.ContentBlocker;
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
            _logger.LogDebug("failed to find torrent {hash} in the download client", hash);
            return result;
        }
        
        result.Found = true;
        
        var properties = await _client.GetTorrentPropertiesAsync(hash);
        result.IsPrivate = properties.IsPrivate;
        
        if (ignoredDownloads.Count > 0 && 
            (download.ShouldIgnore(ignoredDownloads) || properties.TrackerList.Any(x => x.ShouldIgnore(ignoredDownloads))))
        {
            _logger.LogInformation("skip | download is ignored | {name}", download.Name);
            return result;
        }

        var contentBlockerConfig = ContextProvider.Get<ContentBlockerConfig>();
        
        if (contentBlockerConfig.IgnorePrivate && result.IsPrivate)
        {
            // ignore private trackers
            _logger.LogDebug("skip files check | download is private | {name}", download.Name);
            return result;
        }
        
        List<UTorrentFile>? files = await _client.GetTorrentFilesAsync(hash);

        if (files?.Count is null or 0)
        {
            _logger.LogDebug("skip files check | no files found | {name}", download.Name);
            return result;
        }
        
        Dictionary<int, int> priorities = new();
        bool hasPriorityUpdates = false;
        long totalUnwantedFiles = 0;
        
        InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        BlocklistType blocklistType = _blocklistProvider.GetBlocklistType(instanceType);
        ConcurrentBag<string> patterns = _blocklistProvider.GetPatterns(instanceType);
        ConcurrentBag<Regex> regexes = _blocklistProvider.GetRegexes(instanceType);

        for (int i = 0; i < files.Count; i++)
        {
            if (IsDefinitelyMalware(files[i].Name))
            {
                _logger.LogInformation("malware file found | {file} | {title}", files[i].Name, download.Name);
                result.ShouldRemove = true;
                result.DeleteReason = DeleteReason.MalwareFileFound;
                return result;
            }
            
            var file = files[i];
            int priority = file.Priority;

            if (file.Priority == 0) // Already skipped
            {
                totalUnwantedFiles++;
            }

            if (file.Priority != 0 && !_filenameEvaluator.IsValid(file.Name, blocklistType, patterns, regexes))
            {
                totalUnwantedFiles++;
                priority = 0; // Set to skip
                hasPriorityUpdates = true;
                _logger.LogInformation("unwanted file found | {file}", file.Name);
            }
            
            priorities.Add(i, priority);
        }

        if (!hasPriorityUpdates)
        {
            return result;
        }
        
        _logger.LogDebug("changing priorities | torrent {hash}", hash);

        // TODO
        // Convert to array for µTorrent API
        int[] sortedPriorities = new int[priorities.Count];
        for (int i = 0; i < priorities.Count; i++)
        {
            sortedPriorities[i] = priorities[i];
        }

        if (totalUnwantedFiles == files.Count)
        {
            _logger.LogDebug("All files are blocked for {name}", download.Name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesBlocked;
        }

        await _dryRunInterceptor.InterceptAsync(ChangeFilesPriority, hash, sortedPriorities);

        return result;
    }
    
    protected virtual async Task ChangeFilesPriority(string hash, int[] priorities)
    {
        await _client.SetFilePrioritiesAsync(hash, priorities);
    }
} 