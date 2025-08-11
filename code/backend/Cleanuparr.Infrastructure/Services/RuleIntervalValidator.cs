using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Services;

public class RuleIntervalValidator : IRuleIntervalValidator
{
    private readonly ILogger<RuleIntervalValidator> _logger;

    public RuleIntervalValidator(ILogger<RuleIntervalValidator> logger)
    {
        _logger = logger;
    }

    public ValidationResult ValidateStallRuleIntervals(StallRule newRule, List<StallRule> existingRules)
    {
        _logger.LogDebug("Validating stall rule intervals for rule '{RuleName}'", newRule.Name);
        
        var allRules = existingRules.Cast<QueueRule>().ToList();
        allRules.Add(newRule);
        
        return ValidateRuleIntervals(allRules, newRule.Name);
    }

    public ValidationResult ValidateSlowRuleIntervals(SlowRule newRule, List<SlowRule> existingRules)
    {
        _logger.LogDebug("Validating slow rule intervals for rule '{RuleName}'", newRule.Name);
        
        var allRules = existingRules.Cast<QueueRule>().ToList();
        allRules.Add(newRule);
        
        return ValidateRuleIntervals(allRules, newRule.Name);
    }

    public List<IntervalGap> FindGapsInCoverage<T>(List<T> rules) where T : QueueRule
    {
        _logger.LogDebug("Finding gaps in coverage for {RuleCount} rules", rules.Count);
        
        var gaps = new List<IntervalGap>();
        var enabledRules = rules.Where(r => r.Enabled).ToList();
        
        // Find gaps for each privacy type
        gaps.AddRange(FindGapsForPrivacyType(enabledRules, TorrentPrivacyType.Public));
        gaps.AddRange(FindGapsForPrivacyType(enabledRules, TorrentPrivacyType.Private));
        
        _logger.LogDebug("Found {GapCount} gaps in coverage", gaps.Count);
        
        return gaps;
    }

    private ValidationResult ValidateRuleIntervals(List<QueueRule> allRules, string newRuleName)
    {
        // Only consider enabled rules for validation
        var enabledRules = allRules.Where(r => r.Enabled).ToList();
        
        // Expand privacy types (Both -> Public + Private)
        var intervals = ExpandPrivacyTypes(enabledRules);
        
        // Group by privacy type and check for overlaps
        var publicIntervals = intervals.Where(i => i.PrivacyType == TorrentPrivacyType.Public).ToList();
        var privateIntervals = intervals.Where(i => i.PrivacyType == TorrentPrivacyType.Private).ToList();
        
        var publicOverlap = FindOverlappingIntervals(publicIntervals);
        var privateOverlap = FindOverlappingIntervals(privateIntervals);
        
        var errors = new List<string>();
        
        if (publicOverlap != null)
        {
            var message = $"Rule '{newRuleName}' creates overlapping intervals for Public torrents with rule '{publicOverlap.ConflictingRuleName}' (both cover {publicOverlap.OverlapStart}%-{publicOverlap.OverlapEnd}%)";
            errors.Add(message);
            _logger.LogWarning("Overlap detected for Public torrents: {Message}", message);
        }
        
        if (privateOverlap != null)
        {
            var message = $"Rule '{newRuleName}' creates overlapping intervals for Private torrents with rule '{privateOverlap.ConflictingRuleName}' (both cover {privateOverlap.OverlapStart}%-{privateOverlap.OverlapEnd}%)";
            errors.Add(message);
            _logger.LogWarning("Overlap detected for Private torrents: {Message}", message);
        }
        
        if (errors.Any())
        {
            return ValidationResult.Failure("Rule creates overlapping intervals", errors);
        }
        
        return ValidationResult.Success();
    }

    private List<RuleInterval> ExpandPrivacyTypes(List<QueueRule> rules)
    {
        var intervals = new List<RuleInterval>();
        
        foreach (var rule in rules)
        {
            if (rule.PrivacyType == TorrentPrivacyType.Both)
            {
                // Both privacy type creates intervals for both Public and Private
                intervals.Add(new RuleInterval 
                { 
                    PrivacyType = TorrentPrivacyType.Public, 
                    End = rule.MaxCompletionPercentage, 
                    RuleName = rule.Name,
                    RuleId = rule.Id
                });
                intervals.Add(new RuleInterval 
                { 
                    PrivacyType = TorrentPrivacyType.Private, 
                    End = rule.MaxCompletionPercentage, 
                    RuleName = rule.Name,
                    RuleId = rule.Id
                });
            }
            else
            {
                intervals.Add(new RuleInterval 
                { 
                    PrivacyType = rule.PrivacyType, 
                    End = rule.MaxCompletionPercentage, 
                    RuleName = rule.Name,
                    RuleId = rule.Id
                });
            }
        }
        
        return intervals;
    }

    private OverlapResult? FindOverlappingIntervals(List<RuleInterval> intervals)
    {
        if (intervals.Count < 2) return null;
        
        // Sort intervals by end percentage
        var sortedIntervals = intervals.OrderBy(i => i.End).ToList();
        
        for (int i = 1; i < sortedIntervals.Count; i++)
        {
            var current = sortedIntervals[i];
            var previous = sortedIntervals[i - 1];
            
            // Check if current interval starts before previous interval ends
            // Since all intervals start at 0, we check if previous.End >= current.Start (which is 0)
            // But since Start is always 0, we need to check if the intervals actually overlap
            // Two intervals [0, A] and [0, B] overlap if both A > 0 and B > 0 and they're different rules
            
            if (previous.End >= current.Start && previous.RuleId != current.RuleId)
            {
                // Calculate overlap range
                var overlapStart = Math.Max(previous.Start, current.Start);
                var overlapEnd = Math.Min(previous.End, current.End);
                
                return new OverlapResult
                {
                    ConflictingRuleName = previous.RuleName,
                    OverlapStart = overlapStart,
                    OverlapEnd = overlapEnd
                };
            }
        }
        
        return null;
    }

    private List<IntervalGap> FindGapsForPrivacyType<T>(List<T> rules, TorrentPrivacyType privacyType) where T : QueueRule
    {
        var gaps = new List<IntervalGap>();
        
        // Get relevant intervals for this privacy type
        var relevantRules = rules.Where(r => 
            r.PrivacyType == privacyType || 
            r.PrivacyType == TorrentPrivacyType.Both).ToList();
        
        if (!relevantRules.Any())
        {
            // No rules for this privacy type - entire range is a gap
            gaps.Add(new IntervalGap
            {
                PrivacyType = privacyType,
                Start = 0,
                End = 100
            });
            return gaps;
        }
        
        // Sort rules by max completion percentage
        var sortedRules = relevantRules.OrderBy(r => r.MaxCompletionPercentage).ToList();
        
        double currentCovered = 0;
        
        foreach (var rule in sortedRules)
        {
            if (rule.MaxCompletionPercentage > currentCovered)
            {
                if (currentCovered < rule.MaxCompletionPercentage - 0.01) // Avoid tiny gaps due to floating point precision
                {
                    gaps.Add(new IntervalGap
                    {
                        PrivacyType = privacyType,
                        Start = currentCovered,
                        End = rule.MaxCompletionPercentage
                    });
                }
                currentCovered = rule.MaxCompletionPercentage;
            }
        }
        
        // Check if there's a gap from the last rule to 100%
        if (currentCovered < 99.99) // Account for floating point precision
        {
            gaps.Add(new IntervalGap
            {
                PrivacyType = privacyType,
                Start = currentCovered,
                End = 100
            });
        }
        
        return gaps;
    }

    private class OverlapResult
    {
        public string ConflictingRuleName { get; set; } = string.Empty;
        public double OverlapStart { get; set; }
        public double OverlapEnd { get; set; }
    }
}
