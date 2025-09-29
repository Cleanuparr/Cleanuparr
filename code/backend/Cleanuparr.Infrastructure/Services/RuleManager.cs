using Cleanuparr.Domain.Entities;
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
            .ThenBy(r => r.Id) // Use Id as tiebreaker for consistent ordering
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
            .ThenBy(r => r.Id) // Use Id as tiebreaker for consistent ordering
            .ToListAsync();
            
        var readOnlyRules = rules.AsReadOnly();
        
        _cache.Set(SlowRulesCacheKey, readOnlyRules, CacheExpiration);
        
        _logger.LogDebug("Loaded and cached {Count} active slow rules", rules.Count);
        
        return readOnlyRules;
    }
    
    public async Task<IReadOnlyList<TRule>> GetMatchingRulesAsync<TRule>(ITorrentInfo torrent) 
        where TRule : QueueRule
    {
        IReadOnlyList<TRule> allRules;
        
        if (typeof(TRule) == typeof(StallRule))
        {
            allRules = (await GetActiveStallRulesAsync()).Cast<TRule>().ToList().AsReadOnly();
        }
        else if (typeof(TRule) == typeof(SlowRule))
        {
            allRules = (await GetActiveSlowRulesAsync()).Cast<TRule>().ToList().AsReadOnly();
        }
        else
        {
            throw new ArgumentException($"Unsupported rule type: {typeof(TRule).Name}");
        }
        
        var matchingRules = new List<TRule>();
        
        foreach (var rule in allRules)
        {
            try
            {
                if (rule.MatchesTorrent(torrent))
                {
                    matchingRules.Add(rule);
                    _logger.LogDebug("Rule '{ruleName}' matches torrent '{name}'", rule.Name, torrent.Name);
                }
                else
                {
                    _logger.LogTrace("Rule '{ruleName}' does not match torrent '{name}'", rule.Name, torrent.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error evaluating rule '{ruleName}' against torrent '{name}'. Skipping rule.", 
                    rule.Name, torrent.Name
                );
            }
        }
        
        _logger.LogTrace(
            "Found {matching} matching rules out of {total} active rules for torrent '{name}'", 
            matchingRules.Count, allRules.Count, torrent.Name
        );
        
        return matchingRules.AsReadOnly();
    }
}