using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Cache;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Services;

public class RuleEvaluator : IRuleEvaluator
{
    private readonly IRuleManager _ruleManager;
    private readonly IStriker _striker;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions;
    private readonly ILogger<RuleEvaluator> _logger;

    public RuleEvaluator(
        IRuleManager ruleManager,
        IStriker striker,
        IMemoryCache cache,
        ILogger<RuleEvaluator> logger)
    {
        _ruleManager = ruleManager;
        _striker = striker;
        _cache = cache;
        _logger = logger;
        _cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(StaticConfiguration.TriggerValue + Constants.CacheLimitBuffer);
    }

    public async Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient)> EvaluateStallRulesAsync(ITorrentItem torrent)
    {
        _logger.LogTrace("Evaluating stall rules | {name}", torrent.Name);

        // Get matching stall rules in priority order
        var rule = _ruleManager.GetMatchingStallRule(torrent);

        if (rule is null)
        {
            _logger.LogTrace("skip | no stall rules matched | {name}", torrent.Name);
            return (false, DeleteReason.None, false);
        }

        _logger.LogTrace("Applying stall rule {rule} | {name}", rule.Name, torrent.Name);
        ContextProvider.Set<QueueRule>(rule);

        await ResetStalledStrikesAsync(
            torrent,
            rule.ResetStrikesOnProgress,
            rule.MinimumProgressByteSize?.Bytes
        );

        // Apply strike and check if torrent should be removed
        bool shouldRemove = await _striker.StrikeAndCheckLimit(
            torrent.Hash,
            torrent.Name,
            (ushort)rule.MaxStrikes,
            StrikeType.Stalled
        );

        if (shouldRemove)
        {
            return (true, DeleteReason.Stalled, rule.DeletePrivateTorrentsFromClient);
        }

        return (false, DeleteReason.None, false);
    }

    public async Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient)> EvaluateSlowRulesAsync(ITorrentItem torrent)
    {
        _logger.LogTrace("Evaluating slow rules | {name}", torrent.Name);

        // Get matching slow rules in priority order
        SlowRule? rule = _ruleManager.GetMatchingSlowRule(torrent);

        if (rule is null)
        {
            _logger.LogDebug("skip | no slow rules matched | {name}", torrent.Name);
            return (false, DeleteReason.None, false);
        }

        _logger.LogTrace("Applying slow rule {rule} | {name}", rule.Name, torrent.Name);
        ContextProvider.Set<QueueRule>(rule);

        // Check if slow speed
        if (!string.IsNullOrWhiteSpace(rule.MinSpeed))
        {
            ByteSize minSpeed = rule.MinSpeedByteSize;
            ByteSize currentSpeed = new ByteSize(torrent.DownloadSpeed);
            if (currentSpeed.Bytes < minSpeed.Bytes)
            {
                bool shouldRemove = await _striker.StrikeAndCheckLimit(
                    torrent.Hash,
                    torrent.Name,
                    (ushort)rule.MaxStrikes,
                    StrikeType.SlowSpeed
                );

                if (shouldRemove)
                {
                    return (true, DeleteReason.SlowSpeed, rule.DeletePrivateTorrentsFromClient);
                }
            }
            else
            {
                await ResetSlowStrikesAsync(torrent, rule.ResetStrikesOnProgress, StrikeType.SlowSpeed);
            }
        }

        // Check if slow time
        if (rule.MaxTimeHours > 0)
        {
            SmartTimeSpan maxTime = SmartTimeSpan.FromHours(rule.MaxTimeHours);
            SmartTimeSpan currentTime = SmartTimeSpan.FromSeconds(torrent.Eta);
            if (currentTime.Time.TotalSeconds > maxTime.Time.TotalSeconds && maxTime.Time.TotalSeconds > 0)
            {
                bool shouldRemove = await _striker.StrikeAndCheckLimit(
                    torrent.Hash,
                    torrent.Name,
                    (ushort)rule.MaxStrikes,
                    StrikeType.SlowTime
                );

                if (shouldRemove)
                {
                    return (true, DeleteReason.SlowTime, rule.DeletePrivateTorrentsFromClient);
                }
            }
            else
            {
                await ResetSlowStrikesAsync(torrent, rule.ResetStrikesOnProgress, StrikeType.SlowTime);
            }
        }

        return (false, DeleteReason.None, false);
    }

    private async Task ResetStalledStrikesAsync(
        ITorrentItem torrent,
        bool resetEnabled,
        long? minimumProgressBytes
    )
    {
        if (!resetEnabled)
        {
            return;
        }

        if (!HasStalledDownloadProgress(torrent, StrikeType.Stalled, out long previous, out long current))
        {
            _logger.LogTrace("No progress detected | strikes are not reset | {name}", torrent.Name);
            return;
        }

        long progressBytes = current - previous;

        if (minimumProgressBytes is > 0)
        {
            if (progressBytes < minimumProgressBytes)
            {
                _logger.LogTrace(
                    "Progress detected | strikes are not reset | progress: {progress}b | minimum: {minimum}b | {name}",
                    progressBytes,
                    minimumProgressBytes,
                    torrent.Name
                );
                
                return;
            }
            
            _logger.LogTrace(
                "Progress detected | strikes are reset | progress: {progress}b | minimum: {minimum}b | {name}",
                progressBytes,
                minimumProgressBytes,
                torrent.Name
            );
        }
        else
        {
            _logger.LogTrace(
                "Progress detected | strikes are reset | progress: {progress}b | {name}",
                progressBytes,
                torrent.Name
            );
        }

        await _striker.ResetStrikeAsync(torrent.Hash, torrent.Name, StrikeType.Stalled);
    }

    private async Task ResetSlowStrikesAsync(
        ITorrentItem torrent,
        bool resetEnabled,
        StrikeType strikeType
    )
    {
        if (!resetEnabled)
        {
            return;
        }
        
        await _striker.ResetStrikeAsync(torrent.Hash, torrent.Name, strikeType);
    }

    private bool HasStalledDownloadProgress(ITorrentItem torrent, StrikeType strikeType, out long previousDownloaded, out long currentDownloaded)
    {
        previousDownloaded = 0;
        currentDownloaded = Math.Max(0, torrent.DownloadedBytes);

        string cacheKey = CacheKeys.StrikeItem(torrent.Hash, strikeType);

        if (!_cache.TryGetValue(cacheKey, out StalledCacheItem? cachedItem) || cachedItem is null)
        {
            cachedItem = new StalledCacheItem { Downloaded = currentDownloaded };
            _cache.Set(cacheKey, cachedItem, _cacheOptions);
            return false;
        }

        previousDownloaded = cachedItem.Downloaded;

        bool progressed = currentDownloaded > cachedItem.Downloaded;

        if (progressed || currentDownloaded != cachedItem.Downloaded)
        {
            cachedItem.Downloaded = currentDownloaded;
            _cache.Set(cacheKey, cachedItem, _cacheOptions);
        }

        return progressed;
    }
}