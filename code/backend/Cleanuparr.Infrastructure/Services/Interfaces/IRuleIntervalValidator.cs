using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IRuleIntervalValidator
{
    /// <summary>
    /// Validates that a new or updated stall rule doesn't create overlapping intervals
    /// </summary>
    /// <param name="newRule">The rule to validate</param>
    /// <param name="existingRules">Existing rules (excluding the one being updated if applicable)</param>
    /// <returns>Validation result with error details if invalid</returns>
    ValidationResult ValidateStallRuleIntervals(StallRule newRule, List<StallRule> existingRules);
    
    /// <summary>
    /// Validates that a new or updated slow rule doesn't create overlapping intervals
    /// </summary>
    /// <param name="newRule">The rule to validate</param>
    /// <param name="existingRules">Existing rules (excluding the one being updated if applicable)</param>
    /// <returns>Validation result with error details if invalid</returns>
    ValidationResult ValidateSlowRuleIntervals(SlowRule newRule, List<SlowRule> existingRules);
}
