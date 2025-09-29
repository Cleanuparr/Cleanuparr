using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Cache;
using Cleanuparr.Domain.Enums;
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

    public async Task<DownloadCheckResult> EvaluateStallRulesAsync(ITorrentInfo torrent)
    {
        _logger.LogDebug("Evaluating stall rules for torrent '{name}' ({hash})", torrent.Name, torrent.Hash);

        var result = new DownloadCheckResult
        {
            Found = true,
            IsPrivate = torrent.IsPrivate
        };

        // Get matching stall rules in priority order
        var matchingRules = await _ruleManager.GetMatchingRulesAsync<StallRule>(torrent);

        if (matchingRules.Count == 0)
        {
            _logger.LogTrace("No stall rules match torrent '{name}'. No action will be taken.", torrent.Name);
            return result;
        }

        foreach (var rule in matchingRules)
        {
            _logger.LogTrace("Applying stall rule '{ruleName}' to torrent '{name}'", rule.Name, torrent.Name);

            try
            {
                await ResetStrikesIfProgressAsync(
                    torrent,
                    rule.ResetStrikesOnProgress,
                    rule.MinimumProgressByteSize?.Bytes,
                    StrikeType.Stalled,
                    rule.Name
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
                    result.ShouldRemove = true;
                    result.DeleteReason = DeleteReason.Stalled;
                    
                    _logger.LogInformation(
                        "Torrent '{name}' marked for removal by stall rule '{ruleName}' after reaching {strikes} strikes", 
                        torrent.Name, rule.Name, rule.MaxStrikes
                    );
                }
                else
                {
                    _logger.LogDebug(
                        "Strike applied to torrent '{name}' by stall rule '{ruleName}', but removal threshold not reached", 
                        torrent.Name, rule.Name
                    );
                }

                // First-match logic: stop after applying the first matching rule
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error applying stall rule '{ruleName}' to torrent '{name}'. Skipping rule.", 
                    rule.Name, torrent.Name
                );
            }
        }

        return result;
    }

    public async Task<DownloadCheckResult> EvaluateSlowRulesAsync(ITorrentInfo torrent)
    {
        _logger.LogDebug("Evaluating slow rules for torrent '{TorrentName}' ({Hash})", torrent.Name, torrent.Hash);

        var result = new DownloadCheckResult
        {
            Found = true,
            IsPrivate = torrent.IsPrivate
        };

        // Get matching slow rules in priority order
        var matchingRules = await _ruleManager.GetMatchingRulesAsync<SlowRule>(torrent);

        if (matchingRules.Count == 0)
        {
            _logger.LogTrace("No slow rules match torrent '{name}'. No action will be taken.", torrent.Name);
            return result;
        }

        // Apply first-match logic - evaluate rules in priority order and apply the first matching rule
        foreach (var rule in matchingRules)
        {
            var strikeContext = DetermineSlowRuleStrikeContext(rule);

            _logger.LogTrace(
                "Applying slow rule '{ruleName}' ({strikeType}) to torrent '{name}'",
                rule.Name,
                strikeContext.StrikeType,
                torrent.Name);

            try
            {
                await ResetStrikesIfProgressAsync(
                    torrent,
                    rule.ResetStrikesOnProgress,
                    null,
                    strikeContext.StrikeType,
                    rule.Name);
                
                // For slow rules, we need additional torrent information to evaluate speed/time criteria
                // This would typically come from the download client, but for now we'll apply the strike
                // The actual speed/time evaluation would be done by the calling download service
                
                bool shouldRemove = await _striker.StrikeAndCheckLimit(
                    torrent.Hash, 
                    torrent.Name, 
                    (ushort)rule.MaxStrikes, 
                    strikeContext.StrikeType);

                if (shouldRemove)
                {
                    result.ShouldRemove = true;
                    result.DeleteReason = strikeContext.DeleteReason;
                    
                    _logger.LogInformation(
                        "Torrent '{name}' marked for removal by slow rule '{ruleName}' ({strikeType}) after reaching {strikes} strikes", 
                        torrent.Name,
                        rule.Name,
                        strikeContext.StrikeType,
                        rule.MaxStrikes
                    );
                }
                else
                {
                    _logger.LogDebug(
                        "Strike applied to torrent '{name}' by slow rule '{ruleName}' ({strikeType}), but removal threshold not reached", 
                        torrent.Name,
                        rule.Name,
                        strikeContext.StrikeType
                    );
                }

                // First-match logic: stop after applying the first matching rule
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error applying slow rule '{ruleName}' to torrent '{name}'. Skipping rule.", 
                    rule.Name, torrent.Name
                );
            }
        }

        return result;
    }

    private static (StrikeType StrikeType, DeleteReason DeleteReason) DetermineSlowRuleStrikeContext(SlowRule rule)
    {
        bool hasMinSpeed = !string.IsNullOrWhiteSpace(rule.MinSpeed);
        bool hasMaxTime = rule.MaxTimeHours > 0 || rule.MaxTime > 0;

        if (hasMinSpeed && !hasMaxTime)
        {
            return (StrikeType.SlowSpeed, DeleteReason.SlowSpeed);
        }

        if (!hasMinSpeed && hasMaxTime)
        {
            return (StrikeType.SlowTime, DeleteReason.SlowTime);
        }

        if (hasMinSpeed && hasMaxTime)
        {
            // When both are configured treat the slowdown as speed-related to maintain backward compatibility
            return (StrikeType.SlowSpeed, DeleteReason.SlowSpeed);
        }

        // Fallback to SlowSpeed for legacy or misconfigured rules
        return (StrikeType.SlowSpeed, DeleteReason.SlowSpeed);
    }

    private async Task ResetStrikesIfProgressAsync(
        ITorrentInfo torrent,
        bool resetEnabled,
        long? minimumProgressBytes,
        StrikeType strikeType,
        string ruleName)
    {
        if (!resetEnabled)
        {
            return;
        }

        if (!HasDownloadProgress(torrent, strikeType, out long previous, out long current))
        {
            return;
        }

        long progressBytes = current - previous;

        if (minimumProgressBytes.HasValue && minimumProgressBytes.Value > 0 && progressBytes < minimumProgressBytes.Value)
        {
            _logger.LogDebug(
                "Progress detected for torrent '{TorrentName}' while applying {StrikeType} rule '{RuleName}', but {ProgressBytes} bytes is below threshold of {ThresholdBytes} bytes. Strikes remain unchanged.",
                torrent.Name,
                strikeType,
                ruleName,
                progressBytes,
                minimumProgressBytes.Value);
            return;
        }

        _logger.LogDebug(
            "Progress detected for torrent '{TorrentName}' while applying {StrikeType} rule '{RuleName}'. Previous bytes: {Previous}, Current bytes: {Current}. Resetting strikes.",
            torrent.Name,
            strikeType,
            ruleName,
            previous,
            current);

        await _striker.ResetStrikeAsync(torrent.Hash, torrent.Name, strikeType);
    }

    // TODO check and fix
    private bool HasDownloadProgress(ITorrentInfo torrent, StrikeType strikeType, out long previousDownloaded, out long currentDownloaded)
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