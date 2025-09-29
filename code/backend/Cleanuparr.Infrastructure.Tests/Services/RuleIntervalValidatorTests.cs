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
}
