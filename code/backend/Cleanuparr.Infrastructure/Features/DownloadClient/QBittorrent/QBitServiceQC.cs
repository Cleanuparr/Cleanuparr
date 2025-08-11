using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

public partial class QBitService
{
    /// <inheritdoc/>
    public override async Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        DownloadCheckResult result = new();
        TorrentInfo? download = (await _client.GetTorrentListAsync(new TorrentListQuery { Hashes = [hash] }))
            .FirstOrDefault();

        if (download is null)
        {
            _logger.LogDebug("failed to find torrent {hash} in the {name} download client", hash, _downloadClientConfig.Name);
            return result;
        }

        result.Found = true;

        IReadOnlyList<TorrentTracker> trackers = await GetTrackersAsync(hash);

        if (ignoredDownloads.Count > 0 &&
            (download.ShouldIgnore(ignoredDownloads) || trackers.Any(x => x.ShouldIgnore(ignoredDownloads))))
        {
            _logger.LogInformation("skip | download is ignored | {name}", download.Name);
            return result;
        }

        TorrentProperties? torrentProperties = await _client.GetTorrentPropertiesAsync(hash);

        if (torrentProperties is null)
        {
            _logger.LogError("Failed to find torrent properties for {name}", download.Name);
            return result;
        }

        result.IsPrivate = torrentProperties.AdditionalData.TryGetValue("is_private", out var dictValue) &&
                           bool.TryParse(dictValue?.ToString(), out bool boolValue)
                           && boolValue;

        IReadOnlyList<TorrentContent>? files = await _client.GetTorrentContentsAsync(hash);

        if (files?.Count is > 0 && files.All(x => x.Priority is TorrentContentPriority.Skip))
        {
            result.ShouldRemove = true;

            // if all files were blocked by qBittorrent
            if (download is { CompletionOn: not null, Downloaded: null or 0 })
            {
                _logger.LogDebug("all files are unwanted by qBit | removing download | {name}", download.Name);
                result.DeleteReason = DeleteReason.AllFilesSkippedByQBit;
                return result;
            }

            // remove if all files are unwanted
            _logger.LogDebug("all files are unwanted | removing download | {name}", download.Name);
            result.DeleteReason = DeleteReason.AllFilesSkipped;
            return result;
        }

        // Use rule-based evaluation instead of global configuration
        await EvaluateDownloadRemovalWithRules(download, trackers, result);

        return result;
    }
    
    private async Task EvaluateDownloadRemovalWithRules(TorrentInfo torrent, IReadOnlyList<TorrentTracker> trackers, DownloadCheckResult result)
    {
        // Create ITorrentInfo wrapper for rule evaluation
        var torrentInfo = new QBitTorrentInfo(torrent, trackers, result.IsPrivate);
        
        // Evaluate stall rules first
        var stallResult = await _ruleEvaluator.EvaluateStallRulesAsync(torrentInfo);
        if (stallResult.ShouldRemove)
        {
            result.ShouldRemove = true;
            result.DeleteReason = stallResult.DeleteReason;
            return;
        }
        
        // If not stalled, evaluate slow rules
        var slowResult = await _ruleEvaluator.EvaluateSlowRulesAsync(torrentInfo);
        if (slowResult.ShouldRemove)
        {
            result.ShouldRemove = true;
            result.DeleteReason = slowResult.DeleteReason;
        }
    }
}