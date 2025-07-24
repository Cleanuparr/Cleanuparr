using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.ContentBlocker;
using Microsoft.Extensions.Logging;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

public partial class QBitService
{
    /// <inheritdoc/>
    public override async Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        TorrentInfo? download = (await _client.GetTorrentListAsync(new TorrentListQuery { Hashes = [hash] }))
            .FirstOrDefault();
        BlockFilesResult result = new();

        if (download is null)
        {
            _logger.LogDebug("failed to find torrent {hash} in the {name} download client", hash, _downloadClientConfig.Name);
            return result;
        }

        result.Found = true;
        
        IReadOnlyList<TorrentTracker> trackers = await GetTrackersAsync(hash);
        
        if (ignoredDownloads.Count > 0 &&
            (download.ShouldIgnore(ignoredDownloads) || trackers.Any(x => x.ShouldIgnore(ignoredDownloads)) is true))
        {
            _logger.LogInformation("skip | download is ignored | {name}", download.Name);
            return result;
        }
        
        TorrentProperties? torrentProperties = await _client.GetTorrentPropertiesAsync(hash);

        if (torrentProperties is null)
        {
            _logger.LogError("Failed to find torrent properties {name}", download.Name);
            return result;
        }

        bool isPrivate = torrentProperties.AdditionalData.TryGetValue("is_private", out var dictValue) &&
                         bool.TryParse(dictValue?.ToString(), out bool boolValue)
                         && boolValue;

        result.IsPrivate = isPrivate;

        var contentBlockerConfig = ContextProvider.Get<ContentBlockerConfig>();

        if (contentBlockerConfig.IgnorePrivate && isPrivate)
        {
            // ignore private trackers
            _logger.LogDebug("skip files check | download is private | {name}", download.Name);
            return result;
        }
        
        IReadOnlyList<TorrentContent>? files = await _client.GetTorrentContentsAsync(hash);

        if (files?.Count is null or 0)
        {
            _logger.LogDebug("skip files check | no files found | {name}", download.Name);
            return result;
        }

        List<int> unwantedFiles = [];
        long totalFiles = 0;
        long totalUnwantedFiles = 0;
        
        InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        BlocklistType blocklistType = _blocklistProvider.GetBlocklistType(instanceType);
        ConcurrentBag<string> patterns = _blocklistProvider.GetPatterns(instanceType);
        ConcurrentBag<Regex> regexes = _blocklistProvider.GetRegexes(instanceType);
        
        foreach (TorrentContent file in files)
        {
            if (!file.Index.HasValue)
            {
                _logger.LogTrace("Skipping file with no index | {file}", file.Name);
                continue;
            }

            totalFiles++;
            
            if (IsDefinitelyMalware(file.Name))
            {
                _logger.LogInformation("malware file found | {file} | {title}", file.Name, download.Name);
                result.ShouldRemove = true;
                result.DeleteReason = DeleteReason.MalwareFileFound;
                return result;
            }

            if (file.Priority is TorrentContentPriority.Skip)
            {
                _logger.LogTrace("File is already skipped | {file}", file.Name);
                totalUnwantedFiles++;
                continue;
            }

            if (_filenameEvaluator.IsValid(file.Name, blocklistType, patterns, regexes))
            {
                _logger.LogTrace("File is valid | {file}", file.Name);
                continue;
            }
            
            _logger.LogInformation("unwanted file found | {file}", file.Name);
            unwantedFiles.Add(file.Index.Value);
            totalUnwantedFiles++;
        }

        if (unwantedFiles.Count is 0)
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

        foreach (int fileIndex in unwantedFiles)
        {
            await _dryRunInterceptor.InterceptAsync(MarkFileAsSkipped, hash, fileIndex);
        }
        
        return result;
    }
    
    protected virtual async Task MarkFileAsSkipped(string hash, int fileIndex)
    {
        await _client.SetFilePriorityAsync(hash, fileIndex, TorrentContentPriority.Skip);
    }
}