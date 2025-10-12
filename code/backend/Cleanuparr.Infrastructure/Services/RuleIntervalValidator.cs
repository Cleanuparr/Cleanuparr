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
        // Remove duplicate rules with the same ID (keep the last one, which is typically the updated version)
        var deduplicatedRules = allRules
            .GroupBy(r => r.Id)
            .Select(g => g.Last())
            .ToList();

        // Only consider enabled rules for validation
        List<QueueRule> enabledRules = deduplicatedRules
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
        
        List<OverlapResult> publicOverlaps = FindAllOverlappingIntervals(publicIntervals, newRuleName);
        List<OverlapResult> privateOverlaps = FindAllOverlappingIntervals(privateIntervals, newRuleName);

        HashSet<string> overlappingRules = [];

        foreach (var overlap in publicOverlaps)
        {
            overlappingRules.Add(overlap.ConflictingRuleName);

            _logger.LogWarning("Rule {newRuleName} overlaps for Public torrents with rule {ruleName} (both cover {start}%-{end}%)",
                newRuleName,
                overlap.ConflictingRuleName,
                overlap.OverlapStart,
                overlap.OverlapEnd
            );
        }

        foreach (var overlap in privateOverlaps)
        {
            overlappingRules.Add(overlap.ConflictingRuleName);

            _logger.LogWarning("Rule {newRuleName} overlaps for Private torrents with rule {ruleName} (both cover {start}%-{end}%)",
                newRuleName,
                overlap.ConflictingRuleName,
                overlap.OverlapStart,
                overlap.OverlapEnd
            );
        }
        
        if (overlappingRules.Count > 0)
        {
            var details = overlappingRules.ToList();
            return ValidationResult.Failure("Rule creates overlapping intervals with existing rules: " + string.Join(", ", overlappingRules), details);
        }

        return ValidationResult.Success();
    }

    private static List<RuleInterval> ExpandPrivacyTypes(List<QueueRule> rules)
    {
        return rules
            .SelectMany(rule => GetPrivacyTypes(rule.PrivacyType)
                .Select(privacyType => new RuleInterval
                {
                    PrivacyType = privacyType,
                    Start = rule.MinCompletionPercentage,
                    End = rule.MaxCompletionPercentage,
                    RuleName = rule.Name,
                    RuleId = rule.Id
                }))
            .ToList();
    }

    private static IEnumerable<TorrentPrivacyType> GetPrivacyTypes(TorrentPrivacyType type)
    {
        if (type == TorrentPrivacyType.Both)
        {
            yield return TorrentPrivacyType.Public;
            yield return TorrentPrivacyType.Private;
        }
        else
        {
            yield return type;
        }
    }

    /// <summary>
    /// Finds all overlapping intervals for the new rule being validated.
    /// </summary>
    /// <param name="intervals">List of intervals to check for overlaps</param>
    /// <param name="newRuleName">Name of the rule being validated</param>
    /// <returns>List of all overlaps found</returns>
    private static List<OverlapResult> FindAllOverlappingIntervals(List<RuleInterval> intervals, string newRuleName)
    {
        var overlaps = new List<OverlapResult>();

        if (intervals.Count < 2)
        {
            return overlaps;
        }

        var sortedIntervals = intervals
            .OrderBy(i => i.Start)
            .ThenBy(i => i.End)
            .ToList();

        // Find the new rule interval(s)
        var newRuleIntervals = sortedIntervals.Where(i => i.RuleName == newRuleName).ToList();
        var existingIntervals = sortedIntervals.Where(i => i.RuleName != newRuleName).ToList();

        // Check each new rule interval against all existing intervals
        foreach (var newInterval in newRuleIntervals)
        {
            foreach (var existingInterval in existingIntervals)
            {
                // Skip if same rule ID (handles the case where a rule is being updated)
                if (newInterval.RuleId == existingInterval.RuleId)
                {
                    continue;
                }

                // Check for overlap
                if (newInterval.Start < existingInterval.End && existingInterval.Start < newInterval.End)
                {
                    var overlapStart = Math.Max(newInterval.Start, existingInterval.Start);
                    var overlapEnd = Math.Min(newInterval.End, existingInterval.End);

                    if (overlapEnd > overlapStart)
                    {
                        overlaps.Add(new OverlapResult
                        {
                            ConflictingRuleName = existingInterval.RuleName,
                            OverlapStart = overlapStart,
                            OverlapEnd = overlapEnd
                        });
                    }
                }
            }
        }

        return overlaps;
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
            if (interval.Start > currentCoverageEnd)
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

        if (currentCoverageEnd < 100)
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
