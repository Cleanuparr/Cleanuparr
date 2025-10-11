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
        _logger.LogDebug("Validating stall rule intervals for rule {rule}", newRule.Name);
        
        var allRules = existingRules.Cast<QueueRule>().ToList();
        allRules.Add(newRule);
        
        return ValidateRuleIntervals(allRules, newRule.Name);
    }

    public ValidationResult ValidateSlowRuleIntervals(SlowRule newRule, List<SlowRule> existingRules)
    {
        _logger.LogDebug("Validating slow rule intervals for rule {rule}", newRule.Name);
        
        var allRules = existingRules.Cast<QueueRule>().ToList();
        allRules.Add(newRule);
        
        return ValidateRuleIntervals(allRules, newRule.Name);
    }

    public List<IntervalGap> FindGapsInCoverage<T>(List<T> rules) where T : QueueRule
    {
        _logger.LogDebug("Finding gaps in coverage for {rule} rules", rules.Count);
        
        var gaps = new List<IntervalGap>();
        var enabledRules = rules.Where(r => r.Enabled).ToList();
        
        // Find gaps for each privacy type
        gaps.AddRange(FindGapsForPrivacyType(enabledRules, TorrentPrivacyType.Public));
        gaps.AddRange(FindGapsForPrivacyType(enabledRules, TorrentPrivacyType.Private));
        
        _logger.LogDebug("Found {GapCount} gaps in coverage", gaps.Count);
        
        return gaps;
    }

    /// <summary>
    /// Validates that the provided rules do not create overlapping intervals.
    /// </summary>
    /// <param name="allRules">The collection of all rules, including the newly created one.</param>
    /// <param name="newRuleName">The name of the new rule.</param>
    /// <returns></returns>
    private ValidationResult ValidateRuleIntervals(List<QueueRule> allRules, string newRuleName)
    {
        // Only consider enabled rules for validation
        List<QueueRule> enabledRules = allRules
            .Where(r => r.Enabled)
            .ToList();
        
        // Expand privacy types (Both -> Public + Private)
        List<RuleInterval> intervals = ExpandPrivacyTypes(enabledRules);
        
        // Group by privacy type and check for overlaps
        List<RuleInterval> publicIntervals = intervals
            .Where(i => i.PrivacyType == TorrentPrivacyType.Public)
            .ToList();
        List<RuleInterval> privateIntervals = intervals
            .Where(i => i.PrivacyType == TorrentPrivacyType.Private)
            .ToList();
        
        OverlapResult? publicOverlap = FindOverlappingIntervals(publicIntervals);
        OverlapResult? privateOverlap = FindOverlappingIntervals(privateIntervals);
        
        HashSet<string> overlappingRules = [];
        
        if (publicOverlap is not null)
        {
            overlappingRules.Add(publicOverlap.ConflictingRuleName);

            _logger.LogWarning("Rule {newRuleName} overlaps for Public torrents with rule {ruleName} (both cover {start}%-{end}%)",
                newRuleName,
                publicOverlap.ConflictingRuleName,
                publicOverlap.OverlapStart,
                publicOverlap.OverlapEnd
            );
        }
        
        if (privateOverlap is not null)
        {
            overlappingRules.Add(privateOverlap.ConflictingRuleName);

            _logger.LogWarning("Rule {newRuleName} overlaps for Private torrents with rule {ruleName} (both cover {start}%-{end}%)",
                newRuleName,
                privateOverlap.ConflictingRuleName,
                privateOverlap.OverlapStart,
                privateOverlap.OverlapEnd
            );
        }
        
        if (overlappingRules.Count > 0)
        {
            return ValidationResult.Failure("Rule creates overlapping intervals with existing rules: " + string.Join(", ", overlappingRules));
        }
        
        return ValidationResult.Success();
    }

    private static List<RuleInterval> ExpandPrivacyTypes(List<QueueRule> rules)
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
                    Start = rule.MinCompletionPercentage,
                    End = rule.MaxCompletionPercentage, 
                    RuleName = rule.Name,
                    RuleId = rule.Id
                });
                intervals.Add(new RuleInterval 
                { 
                    PrivacyType = TorrentPrivacyType.Private, 
                    Start = rule.MinCompletionPercentage,
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
                    Start = rule.MinCompletionPercentage,
                    End = rule.MaxCompletionPercentage, 
                    RuleName = rule.Name,
                    RuleId = rule.Id
                });
            }
        }
        
        return intervals;
    }

    private static OverlapResult? FindOverlappingIntervals(List<RuleInterval> intervals)
    {
        if (intervals.Count < 2)
        {
            return null;
        }

        var sortedIntervals = intervals
            .OrderBy(i => i.Start)
            .ThenBy(i => i.End)
            .ToList();

        var active = sortedIntervals[0];

        for (int i = 1; i < sortedIntervals.Count; i++)
        {
            var current = sortedIntervals[i];

            if (current.Start < active.End && active.RuleId != current.RuleId)
            {
                var overlapStart = Math.Max(active.Start, current.Start);
                var overlapEnd = Math.Min(active.End, current.End);

                if (overlapEnd > overlapStart)
                {
                    return new OverlapResult
                    {
                        ConflictingRuleName = active.RuleName,
                        OverlapStart = overlapStart,
                        OverlapEnd = overlapEnd
                    };
                }
            }

            if (current.End > active.End)
            {
                active = current;
            }
        }

        return null;
    }

    private static List<IntervalGap> FindGapsForPrivacyType<T>(List<T> rules, TorrentPrivacyType privacyType) where T : QueueRule
    {
        var gaps = new List<IntervalGap>();
        
        // Get relevant intervals for this privacy type
        var relevantRules = rules.Where(r => 
            r.PrivacyType == privacyType || 
            r.PrivacyType == TorrentPrivacyType.Both).ToList();
        
        if (!relevantRules.Any())
        {
            gaps.Add(new IntervalGap
            {
                PrivacyType = privacyType,
                Start = 0,
                End = 100
            });
            return gaps;
        }

        var intervals = relevantRules
            .Select(r => new
            {
                Start = Math.Max(0, Math.Min(100, (int)r.MinCompletionPercentage)),
                End = Math.Max(0, Math.Min(100, (int)r.MaxCompletionPercentage))
            })
            .Where(i => i.End >= i.Start)
            .OrderBy(i => i.Start)
            .ThenBy(i => i.End)
            .ToList();

        double currentCoverageEnd = 0;

        foreach (var interval in intervals)
        {
            if (interval.Start > currentCoverageEnd + 0.0001)
            {
                gaps.Add(new IntervalGap
                {
                    PrivacyType = privacyType,
                    Start = currentCoverageEnd,
                    End = interval.Start
                });
            }

            if (interval.End > currentCoverageEnd)
            {
                currentCoverageEnd = interval.End;
            }

            if (currentCoverageEnd >= 100)
            {
                break;
            }
        }

        if (currentCoverageEnd < 100 - 0.0001)
        {
            gaps.Add(new IntervalGap
            {
                PrivacyType = privacyType,
                Start = currentCoverageEnd,
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
