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

        List<QueueRule> enabledRules = deduplicatedRules
            .Where(r => r.Enabled)
            .ToList();
        
        // Expand privacy types (Both -> Public + Private) and content types (All -> New + Upgrade)
        List<RuleInterval> intervals = ExpandRuleAxes(enabledRules);

        // Group by privacy type and content type, then check for overlaps within each group
        HashSet<string> overlappingRules = [];

        foreach (var group in intervals.GroupBy(i => (i.PrivacyType, i.ContentType)))
        {
            List<OverlapResult> overlaps = FindAllOverlappingIntervals(group.ToList(), newRuleName);

            foreach (var overlap in overlaps)
            {
                overlappingRules.Add(overlap.ConflictingRuleName);

                _logger.LogWarning("Rule {newRuleName} overlaps for {privacyType} {contentType} torrents with rule {ruleName} (both cover {start}%-{end}%)",
                    newRuleName,
                    group.Key.PrivacyType,
                    group.Key.ContentType,
                    overlap.ConflictingRuleName,
                    overlap.OverlapStart,
                    overlap.OverlapEnd
                );
            }
        }

        if (overlappingRules.Count > 0)
        {
            var details = overlappingRules.ToList();
            return ValidationResult.Failure("Rule creates overlapping intervals with existing rules: " + string.Join(", ", overlappingRules), details);
        }

        return ValidationResult.Success();
    }

    private static List<RuleInterval> ExpandRuleAxes(List<QueueRule> rules)
    {
        return rules
            .SelectMany(rule => GetPrivacyTypes(rule.PrivacyType)
                .SelectMany(privacyType => GetContentTypes(rule.ContentType)
                    .Select(contentType => new RuleInterval
                    {
                        PrivacyType = privacyType,
                        ContentType = contentType,
                        Start = rule.MinCompletionPercentage,
                        End = rule.MaxCompletionPercentage,
                        RuleName = rule.Name,
                        RuleId = rule.Id
                    })))
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

    private static IEnumerable<QueueRuleContentType> GetContentTypes(QueueRuleContentType type)
    {
        if (type == QueueRuleContentType.All)
        {
            yield return QueueRuleContentType.New;
            yield return QueueRuleContentType.Upgrade;
        }
        else
        {
            yield return type;
        }
    }

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

    private class OverlapResult
    {
        public string ConflictingRuleName { get; set; } = string.Empty;
        public double OverlapStart { get; set; }
        public double OverlapEnd { get; set; }
    }
}
