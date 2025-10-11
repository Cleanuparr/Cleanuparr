using System;
using System.Collections.Generic;
using System.Linq;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class RuleIntervalValidatorTests
{
    private readonly RuleIntervalValidator _validator;

    public RuleIntervalValidatorTests()
    {
        var logger = Mock.Of<ILogger<RuleIntervalValidator>>();
        _validator = new RuleIntervalValidator(logger);
    }

    [Fact]
    public void ValidateStallRuleIntervals_ReturnsFailureWhenOverlapDetected()
    {
        var existingRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 50
        };

        var newRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "New",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 40,
            MaxCompletionPercentage = 60
        };

        var result = _validator.ValidateStallRuleIntervals(newRule, new List<StallRule> { existingRule });

        result.IsValid.ShouldBeFalse();
        result.Details.ShouldNotBeEmpty();
        result.ErrorMessage.ShouldNotBeNull();
    }

    [Fact]
    public void ValidateStallRuleIntervals_AllowsAdjacentRanges()
    {
        var firstRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "First",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 40
        };

        var newRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "Second",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 41,
            MaxCompletionPercentage = 80
        };

        var result = _validator.ValidateStallRuleIntervals(newRule, new List<StallRule> { firstRule });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateStallRuleIntervals_AllowsTouchingRanges()
    {
        var firstRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "First",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 40
        };

        var newRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "Second",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 40,
            MaxCompletionPercentage = 80
        };

        var result = _validator.ValidateStallRuleIntervals(newRule, new List<StallRule> { firstRule });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateStallRuleIntervals_AllowsZeroWidthIntervalInsideExistingRange()
    {
        var existingRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 10,
            MaxCompletionPercentage = 40
        };

        var newRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "ZeroWidth",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 30,
            MaxCompletionPercentage = 30
        };

        var result = _validator.ValidateStallRuleIntervals(newRule, new List<StallRule> { existingRule });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void FindGapsInCoverage_ReturnsFullGapWhenNoRules()
    {
        var gaps = _validator.FindGapsInCoverage(new List<StallRule>());

        gaps.ShouldNotBeEmpty();
        gaps.Count(g => g.PrivacyType == TorrentPrivacyType.Public).ShouldBe(1);
        gaps.Count(g => g.PrivacyType == TorrentPrivacyType.Private).ShouldBe(1);

        gaps.First(g => g.PrivacyType == TorrentPrivacyType.Public).ShouldSatisfyAllConditions(
            gap => gap.Start.ShouldBe(0),
            gap => gap.End.ShouldBe(100)
        );
    }

    [Fact]
    public void FindGapsInCoverage_UsesMinimumBoundaries()
    {
        var rules = new List<StallRule>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Partial",
                Enabled = true,
                MaxStrikes = 3,
                PrivacyType = TorrentPrivacyType.Public,
                MinCompletionPercentage = 0,
                MaxCompletionPercentage = 40
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Upper",
                Enabled = true,
                MaxStrikes = 3,
                PrivacyType = TorrentPrivacyType.Public,
                MinCompletionPercentage = 60,
                MaxCompletionPercentage = 90
            }
        };

        var gaps = _validator.FindGapsInCoverage(rules);

        var publicGap = gaps.FirstOrDefault(g => g.PrivacyType == TorrentPrivacyType.Public && g.Start >= 40 && g.End <= 60);
        publicGap.ShouldNotBeNull();
        publicGap!.Start.ShouldBe(40);
        publicGap.End.ShouldBe(60);

        var privateGap = gaps.First(g => g.PrivacyType == TorrentPrivacyType.Private);
        privateGap.Start.ShouldBe(0);
        privateGap.End.ShouldBe(100);
    }

    [Fact]
    public void ValidateSlowRuleIntervals_AllowsTouchingRanges()
    {
        var firstRule = new SlowRule
        {
            Id = Guid.NewGuid(),
            Name = "First Slow",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 40,
            ResetStrikesOnProgress = false,
            MaxTimeHours = 1,
            MinSpeed = "1 MB"
        };

        var newRule = new SlowRule
        {
            Id = Guid.NewGuid(),
            Name = "Second Slow",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 40,
            MaxCompletionPercentage = 80,
            ResetStrikesOnProgress = false,
            MaxTimeHours = 1,
            MinSpeed = "1 MB"
        };

        var result = _validator.ValidateSlowRuleIntervals(newRule, new List<SlowRule> { firstRule });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateStallRuleIntervals_DetectsOverlapWithBothRule()
    {
        var existingRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "Both Existing",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Both,
            MinCompletionPercentage = 20,
            MaxCompletionPercentage = 60
        };

        var newRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "Public New",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 50,
            MaxCompletionPercentage = 70
        };

        var result = _validator.ValidateStallRuleIntervals(newRule, new List<StallRule> { existingRule });

        result.IsValid.ShouldBeFalse();
        result.Details.ShouldNotBeEmpty();
    }

    [Fact]
    public void ValidateSlowRuleIntervals_DetectsOverlap()
    {
        var existingRule = new SlowRule
        {
            Id = Guid.NewGuid(),
            Name = "Existing Slow",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 10,
            MaxCompletionPercentage = 50,
            ResetStrikesOnProgress = false,
            MaxTimeHours = 1,
            MinSpeed = "1 MB"
        };

        var newRule = new SlowRule
        {
            Id = Guid.NewGuid(),
            Name = "New Slow",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 40,
            MaxCompletionPercentage = 80,
            ResetStrikesOnProgress = false,
            MaxTimeHours = 1,
            MinSpeed = "1 MB"
        };

        var result = _validator.ValidateSlowRuleIntervals(newRule, new List<SlowRule> { existingRule });

        result.IsValid.ShouldBeFalse();
        result.Details.ShouldNotBeEmpty();
    }

    [Fact]
    public void FindGapsInCoverage_NoGapsWhenFullyCovered()
    {
        var rules = new List<StallRule>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Lower",
                Enabled = true,
                MaxStrikes = 3,
                PrivacyType = TorrentPrivacyType.Public,
                MinCompletionPercentage = 0,
                MaxCompletionPercentage = 50
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Upper",
                Enabled = true,
                MaxStrikes = 3,
                PrivacyType = TorrentPrivacyType.Public,
                MinCompletionPercentage = 50,
                MaxCompletionPercentage = 100
            }
        };

        var gaps = _validator.FindGapsInCoverage(rules);

        // No public gaps expected
        gaps.Count(g => g.PrivacyType == TorrentPrivacyType.Public).ShouldBe(0);
    }

    [Fact]
    public void FindGapsInCoverage_NoGapsWhenBothRuleCoversAll()
    {
        var rules = new List<StallRule>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "BothCoverage",
                Enabled = true,
                MaxStrikes = 3,
                PrivacyType = TorrentPrivacyType.Both,
                MinCompletionPercentage = 0,
                MaxCompletionPercentage = 100
            }
        };

        var gaps = _validator.FindGapsInCoverage(rules);

        gaps.Count(g => g.PrivacyType == TorrentPrivacyType.Public).ShouldBe(0);
        gaps.Count(g => g.PrivacyType == TorrentPrivacyType.Private).ShouldBe(0);
    }

    [Fact]
    public void FindGapsInCoverage_ClampsBounds()
    {
        var rules = new List<StallRule>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "OutOfRange",
                Enabled = true,
                MaxStrikes = 3,
                PrivacyType = TorrentPrivacyType.Public,
                MinCompletionPercentage = 20,
                MaxCompletionPercentage = 150
            }
        };

        var gaps = _validator.FindGapsInCoverage(rules);

        var publicGap = gaps.FirstOrDefault(g => g.PrivacyType == TorrentPrivacyType.Public);
        publicGap.ShouldNotBeNull();
        publicGap!.Start.ShouldBe(0);
        publicGap.End.ShouldBe(20);
    }

    [Fact]
    public void ValidateStallRuleIntervals_IgnoresDisabledRules()
    {
        var existingRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "Disabled",
            Enabled = false,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 30,
            MaxCompletionPercentage = 70
        };

        var newRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "New",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 40,
            MaxCompletionPercentage = 60
        };

        var result = _validator.ValidateStallRuleIntervals(newRule, new List<StallRule> { existingRule });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateStallRuleIntervals_IgnoresOverlapWhenSameRuleId()
    {
        var id = Guid.NewGuid();

        var existingRule = new StallRule
        {
            Id = id,
            Name = "Existing",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 10,
            MaxCompletionPercentage = 50
        };

        // New rule uses the same Id as the existing rule; overlaps should be ignored
        var newRule = new StallRule
        {
            Id = id,
            Name = "NewSameId",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 40,
            MaxCompletionPercentage = 80
        };

        var result = _validator.ValidateStallRuleIntervals(newRule, new List<StallRule> { existingRule });

        // Since both rules share the same Id, they are considered the same rule and should not be treated as overlapping
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateStallRuleIntervals_DetectsOverlapInBothPublicAndPrivate()
    {
        var existingPublic = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "ExistingPublic",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 10,
            MaxCompletionPercentage = 30
        };

        var existingPrivate = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "ExistingPrivate",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Private,
            MinCompletionPercentage = 15,
            MaxCompletionPercentage = 35
        };

        // New rule applies to both privacy types and overlaps both existing rules
        var newRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "NewBoth",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Both,
            MinCompletionPercentage = 20,
            MaxCompletionPercentage = 25
        };

        var result = _validator.ValidateStallRuleIntervals(newRule, new List<StallRule> { existingPublic, existingPrivate });

        result.IsValid.ShouldBeFalse();
        // Expect at least two overlap details (one for public, one for private)
        result.Details.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void ValidateStallRuleIntervals_DetectsPrivateOverlap()
    {
        var existingRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "ExistingPrivate",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Private,
            MinCompletionPercentage = 20,
            MaxCompletionPercentage = 60
        };

        var newRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "NewPrivate",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Private,
            MinCompletionPercentage = 50,
            MaxCompletionPercentage = 80
        };

        var result = _validator.ValidateStallRuleIntervals(newRule, new List<StallRule> { existingRule });

        result.IsValid.ShouldBeFalse();
        result.Details.ShouldNotBeEmpty();
    }

    [Fact]
    public void ValidateStallRuleIntervals_SucceedsWithOnlyNewRuleEnabled()
    {
        var newRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "OnlyNew",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 10,
            MaxCompletionPercentage = 90
        };

        // No existing rules
        var result = _validator.ValidateStallRuleIntervals(newRule, new List<StallRule>());

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateStallRuleIntervals_AllowsNonOverlappingUnsortedRules()
    {
        var r1 = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "R1",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 60,
            MaxCompletionPercentage = 70
        };

        var r2 = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "R2",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 10
        };

        var newRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "New",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 20,
            MaxCompletionPercentage = 50
        };

        // Pass existing rules in unsorted order to exercise sorting inside the validator
        var result = _validator.ValidateStallRuleIntervals(newRule, new List<StallRule> { r1, r2 });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void FindGapsInCoverage_IgnoresInvalidIntervalsWhereMaxLessThanMin()
    {
        var rules = new List<StallRule>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Invalid",
                Enabled = true,
                MaxStrikes = 3,
                PrivacyType = TorrentPrivacyType.Public,
                MinCompletionPercentage = 80,
                MaxCompletionPercentage = 20
            }
        };

        // Invalid interval should be ignored, resulting in full gap
        var gaps = _validator.FindGapsInCoverage(rules);

        var publicGap = gaps.First(g => g.PrivacyType == TorrentPrivacyType.Public);
        publicGap.Start.ShouldBe(0);
        publicGap.End.ShouldBe(100);
    }

    [Fact]
    public void FindGapsInCoverage_IgnoresDisabledRulesWhenCalculatingCoverage()
    {
        var rules = new List<StallRule>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Disabled",
                Enabled = false,
                MaxStrikes = 3,
                PrivacyType = TorrentPrivacyType.Public,
                MinCompletionPercentage = 0,
                MaxCompletionPercentage = 100
            }
        };

        var gaps = _validator.FindGapsInCoverage(rules);

        gaps.Count(g => g.PrivacyType == TorrentPrivacyType.Public).ShouldBe(1);
        gaps.Count(g => g.PrivacyType == TorrentPrivacyType.Private).ShouldBe(1);

        var publicGap = gaps.First(g => g.PrivacyType == TorrentPrivacyType.Public);
        publicGap.Start.ShouldBe(0);
        publicGap.End.ShouldBe(100);

        var privateGap = gaps.First(g => g.PrivacyType == TorrentPrivacyType.Private);
        privateGap.Start.ShouldBe(0);
        privateGap.End.ShouldBe(100);
    }

    [Fact]
    public void ValidateStallRuleIntervals_DoesNotReportSelfOverlapWhenEnablingDisabledRule()
    {
        var ruleId = Guid.NewGuid();

        // Simulate existing disabled rule
        var disabledRule = new StallRule
        {
            Id = ruleId,
            Name = "Rule1",
            Enabled = false,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Both,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100
        };

        // Simulate another existing enabled rule
        var enabledRule = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "Rule2",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Both,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100
        };

        // Now we enable the first rule (same ID, but now enabled)
        var nowEnabledRule = new StallRule
        {
            Id = ruleId,
            Name = "Rule1",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Both,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100
        };

        // Validation should detect overlap with Rule2, not with itself
        var result = _validator.ValidateStallRuleIntervals(nowEnabledRule, new List<StallRule> { disabledRule, enabledRule });

        result.IsValid.ShouldBeFalse();
        // The error should mention Rule2, not Rule1
        result.ErrorMessage.ShouldContain("Rule2");
        result.ErrorMessage.ShouldNotContain("Rule1");
    }

    [Fact]
    public void ValidateStallRuleIntervals_BothPrivacyTypeDoesNotSelfOverlap()
    {
        var ruleId = Guid.NewGuid();

        // Create a single rule with Both privacy type
        var newRule = new StallRule
        {
            Id = ruleId,
            Name = "BothRule",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Both,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100
        };

        // Validate with no existing rules - should pass
        var result = _validator.ValidateStallRuleIntervals(newRule, new List<StallRule>());

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateStallRuleIntervals_DetectsMultipleOverlaps()
    {
        // Rule1 covers 0-80%
        var rule1 = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "Rule1",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 80
        };

        // Rule2 covers 80-100%
        var rule2 = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "Rule2",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 80,
            MaxCompletionPercentage = 100
        };

        // Rule3 overlaps both Rule1 and Rule2 (0-90%)
        var rule3 = new StallRule
        {
            Id = Guid.NewGuid(),
            Name = "Rule3",
            Enabled = true,
            MaxStrikes = 3,
            PrivacyType = TorrentPrivacyType.Public,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 90
        };

        var result = _validator.ValidateStallRuleIntervals(rule3, new List<StallRule> { rule1, rule2 });

        result.IsValid.ShouldBeFalse();
        // Should detect overlaps with both Rule1 and Rule2
        result.Details.Count.ShouldBe(2);
        result.Details.ShouldContain("Rule1");
        result.Details.ShouldContain("Rule2");
        result.ErrorMessage.ShouldContain("Rule1");
        result.ErrorMessage.ShouldContain("Rule2");
    }
}
