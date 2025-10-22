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
        
        var malwareBlockerConfig = ContextProvider.Get<ContentBlockerConfig>();
        
        if (malwareBlockerConfig.IgnorePrivate && isPrivate)
        {
            // ignore private trackers
            _logger.LogDebug("skip files check | download is private | {name}", download.Name);
            return result;
        }

        List<long> unwantedFiles = [];
        long totalFiles = 0;
        long totalUnwantedFiles = 0;
        
        InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        BlocklistType blocklistType = _blocklistProvider.GetBlocklistType(instanceType);
        ConcurrentBag<string> patterns = _blocklistProvider.GetPatterns(instanceType);
        ConcurrentBag<Regex> regexes = _blocklistProvider.GetRegexes(instanceType);
        ConcurrentBag<string> malwarePatterns = _blocklistProvider.GetMalwarePatterns();
        
        for (int i = 0; i < download.Files.Length; i++)
        {
            if (download.FileStats?[i].Wanted == null)
            {
                _logger.LogTrace("Skipping file with no stats | {file}", download.Files[i].Name);
                continue;
            }

            totalFiles++;
            
            if (malwareBlockerConfig.DeleteKnownMalware && _filenameEvaluator.IsKnownMalware(download.Files[i].Name, malwarePatterns))
            {
                _logger.LogInformation("malware file found | {file} | {title}", download.Files[i].Name, download.Name);
                result.ShouldRemove = true;
                result.DeleteReason = DeleteReason.MalwareFileFound;
                return result;
            }
            
            if (!download.FileStats[i].Wanted.Value)
            {
                _logger.LogTrace("File is already skipped | {file}", download.Files[i].Name);
                totalUnwantedFiles++;
                continue;
            }

            if (_filenameEvaluator.IsValid(download.Files[i].Name, blocklistType, patterns, regexes))
            {
                _logger.LogTrace("File is valid | {file}", download.Files[i].Name);
                continue;
            }
            
            _logger.LogInformation("unwanted file found | {file}", download.Files[i].Name);
            unwantedFiles.Add(i);
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

        await _dryRunInterceptor.InterceptAsync(SetUnwantedFiles, download.Id, unwantedFiles.ToArray());

        return result;
    }
}