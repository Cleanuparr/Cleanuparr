using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Services;

public class RuleManager : IRuleManager
{
    private readonly DataContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RuleManager> _logger;
    
    private const string StallRulesCacheKey = "active_stall_rules";
    private const string SlowRulesCacheKey = "active_slow_rules";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);
    
    public RuleManager(DataContext context, IMemoryCache cache, ILogger<RuleManager> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<IReadOnlyList<StallRule>> GetActiveStallRulesAsync()
    {
        if (_cache.TryGetValue(StallRulesCacheKey, out IReadOnlyList<StallRule>? cachedRules))
        {
            _logger.LogDebug("Retrieved {Count} stall rules from cache", cachedRules!.Count);
            return cachedRules;
        }
        
        _logger.LogDebug("Loading stall rules from database");
        
        var rules = await _context.StallRules
            .Where(r => r.Enabled)
            .OrderByDescending(r => r.MaxCompletionPercentage)
            .ThenByDescending(r => r.MinCompletionPercentage)
            .ToListAsync();
            
        var readOnlyRules = rules.AsReadOnly();
        
        _cache.Set(StallRulesCacheKey, readOnlyRules, CacheExpiration);
        
        _logger.LogDebug("Loaded and cached {Count} active stall rules", rules.Count);
        
        return readOnlyRules;
    }
    
    public async Task<IReadOnlyList<SlowRule>> GetActiveSlowRulesAsync()
    {
        if (_cache.TryGetValue(SlowRulesCacheKey, out IReadOnlyList<SlowRule>? cachedRules))
        {
            _logger.LogDebug("Retrieved {Count} slow rules from cache", cachedRules!.Count);
            return cachedRules;
        }
        
        _logger.LogDebug("Loading slow rules from database");
        
        var rules = await _context.SlowRules
            .Where(r => r.Enabled)
            .OrderByDescending(r => r.MaxCompletionPercentage)
            .ThenByDescending(r => r.MinCompletionPercentage)
            .ToListAsync();
            
        var readOnlyRules = rules.AsReadOnly();
        
        _cache.Set(SlowRulesCacheKey, readOnlyRules, CacheExpiration);
        
        _logger.LogDebug("Loaded and cached {Count} active slow rules", rules.Count);
        
        return readOnlyRules;
    }
    
    public Task<StallRule?> GetMatchingStallRuleAsync(ITorrentInfo torrent)
    {
        var stallRules = ContextProvider.Get<List<StallRule>>(nameof(StallRule)) ?? new List<StallRule>();
        var match = GetMatchingRule(torrent, stallRules);
        return Task.FromResult(match);
    }

    public Task<SlowRule?> GetMatchingSlowRuleAsync(ITorrentInfo torrent)
    {
        var slowRules = ContextProvider.Get<List<SlowRule>>(nameof(SlowRule)) ?? new List<SlowRule>();
        var match = GetMatchingRule(torrent, slowRules);
        return Task.FromResult(match);
    }

    private TRule? GetMatchingRule<TRule>(ITorrentInfo torrent, IReadOnlyList<TRule> rules) where TRule : QueueRule
    {
        if (rules.Count == 0)
        {
            _logger.LogTrace(
                "No active {RuleType} rules available to evaluate torrent '{name}'",
                typeof(TRule).Name,
                torrent.Name);
            return null;
        }

        TRule? matchedRule = null;

        foreach (var rule in rules)
        {
            try
            {
                if (rule.MatchesTorrent(torrent))
                {
                    if (matchedRule is null)
                    {
                        matchedRule = rule;
                        _logger.LogDebug(
                            "Rule '{ruleName}' matches torrent '{name}'",
                            rule.Name,
                            torrent.Name);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Multiple {RuleType} rules matched torrent '{TorrentName}'. Using rule '{SelectedRule}' and ignoring '{IgnoredRule}'.",
                            typeof(TRule).Name,
                            torrent.Name,
                            matchedRule.Name,
                            rule.Name);
                    }
                }
                else
                {
                    _logger.LogTrace(
                        "Rule '{ruleName}' does not match torrent '{name}'",
                        rule.Name,
                        torrent.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error evaluating rule '{ruleName}' against torrent '{name}'. Skipping rule.",
                    rule.Name,
                    torrent.Name);
            }
        }

        if (matchedRule is null)
        {
            _logger.LogTrace(
                "No matching {RuleType} rules found for torrent '{name}'",
                typeof(TRule).Name,
                torrent.Name);
        }

        return matchedRule;
    }
}