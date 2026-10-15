using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.QueueCleaner;

/// <summary>
/// Tests for the abstract QueueRule base class validation logic.
/// Uses StallRule as a concrete implementation for testing.
/// </summary>
public sealed class QueueRuleTests
{
    #region Validate - Valid Configurations

    [Fact]
    public void Validate_WithValidConfig_DoesNotThrow()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Fact]
    public void Validate_WithMinimumValidMaxStrikes_DoesNotThrow()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100
        };

        Should.NotThrow(() => rule.Validate());
    }

    #endregion

    #region Validate - Name Validation

    [Fact]
    public void Validate_WithEmptyName_ThrowsValidationException()
    {
        var rule = new StallRule
        {
            Name = "",
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100
        };

        var exception = Should.Throw<CustomValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Rule name cannot be empty");
    }

    [Fact]
    public void Validate_WithWhitespaceName_ThrowsValidationException()
    {
        var rule = new StallRule
        {
            Name = "   ",
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100
        };

        var exception = Should.Throw<CustomValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Rule name cannot be empty");
    }

    [Fact]
    public void Validate_WithTabOnlyName_ThrowsValidationException()
    {
        var rule = new StallRule
        {
            Name = "\t",
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100
        };

        var exception = Should.Throw<CustomValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Rule name cannot be empty");
    }

    #endregion

    #region Validate - MaxStrikes Validation

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Validate_WithMaxStrikesLessThan3_ThrowsValidationException(int maxStrikes)
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = maxStrikes,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100
        };

        var exception = Should.Throw<CustomValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Max strikes must be at least 3");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(100)]
    public void Validate_WithValidMaxStrikes_DoesNotThrow(int maxStrikes)
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = maxStrikes,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 100
        };

        Should.NotThrow(() => rule.Validate());
    }

    #endregion

    #region Validate - MinCompletionPercentage Validation

    [Theory]
    [InlineData((ushort)101)]
    [InlineData((ushort)150)]
    [InlineData((ushort)255)]
    public void Validate_WithMinCompletionPercentageExceeding100_ThrowsValidationException(ushort minCompletionPercentage)
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinCompletionPercentage = minCompletionPercentage,
            MaxCompletionPercentage = 100
        };

        var exception = Should.Throw<CustomValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Minimum completion percentage must be between 0 and 100");
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)50)]
    [InlineData((ushort)100)]
    public void Validate_WithValidMinCompletionPercentage_DoesNotThrow(ushort minCompletionPercentage)
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinCompletionPercentage = minCompletionPercentage,
            MaxCompletionPercentage = 100
        };

        Should.NotThrow(() => rule.Validate());
    }

    #endregion

    #region Validate - MaxCompletionPercentage Validation

    [Theory]
    [InlineData((ushort)101)]
    [InlineData((ushort)150)]
    [InlineData((ushort)255)]
    public void Validate_WithMaxCompletionPercentageExceeding100_ThrowsValidationException(ushort maxCompletionPercentage)
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = maxCompletionPercentage
        };

        var exception = Should.Throw<CustomValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Maximum completion percentage must be between 1 and 100");
    }

    [Fact]
    public void Validate_WithZeroMaxCompletionPercentage_ThrowsValidationException()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = 0
        };

        var exception = Should.Throw<CustomValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Maximum completion percentage must be greater than 0");
    }

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)50)]
    [InlineData((ushort)100)]
    public void Validate_WithValidMaxCompletionPercentage_DoesNotThrow(ushort maxCompletionPercentage)
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinCompletionPercentage = 0,
            MaxCompletionPercentage = maxCompletionPercentage
        };

        Should.NotThrow(() => rule.Validate());
    }

    #endregion

    #region Validate - Completion Percentage Range Validation

    [Fact]
    public void Validate_WithMaxLessThanMin_ThrowsValidationException()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinCompletionPercentage = 50,
            MaxCompletionPercentage = 25
        };

        var exception = Should.Throw<CustomValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Maximum completion percentage must be greater than or equal to the minimum completion percentage");
    }

    [Fact]
    public void Validate_WithMaxEqualToMin_DoesNotThrow()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinCompletionPercentage = 50,
            MaxCompletionPercentage = 50
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Fact]
    public void Validate_WithMaxGreaterThanMin_DoesNotThrow()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinCompletionPercentage = 25,
            MaxCompletionPercentage = 75
        };

        Should.NotThrow(() => rule.Validate());
    }

    #endregion

    #region PrivacyType Tests

    [Theory]
    [InlineData(TorrentPrivacyType.Public)]
    [InlineData(TorrentPrivacyType.Private)]
    [InlineData(TorrentPrivacyType.Both)]
    public void PrivacyType_WithDifferentValues_SetsCorrectly(TorrentPrivacyType privacyType)
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            PrivacyType = privacyType
        };

        rule.PrivacyType.ShouldBe(privacyType);
    }

    #endregion

    #region MatchesContentType

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void MatchesContentType_All_AlwaysMatches(bool? isUpgrade)
    {
        var rule = new StallRule { Name = "test-rule", ContentType = QueueRuleContentType.All };

        rule.MatchesContentType(isUpgrade).ShouldBeTrue();
    }

    [Fact]
    public void MatchesContentType_New_MatchesFalseOnly()
    {
        var rule = new StallRule { Name = "test-rule", ContentType = QueueRuleContentType.New };

        rule.MatchesContentType(false).ShouldBeTrue();
        rule.MatchesContentType(true).ShouldBeFalse();
        rule.MatchesContentType(null).ShouldBeFalse();
    }

    [Fact]
    public void MatchesContentType_Upgrade_MatchesTrueOnly()
    {
        var rule = new StallRule { Name = "test-rule", ContentType = QueueRuleContentType.Upgrade };

        rule.MatchesContentType(true).ShouldBeTrue();
        rule.MatchesContentType(false).ShouldBeFalse();
        rule.MatchesContentType(null).ShouldBeFalse();
    }

    #endregion

    #region Validate - ChangeCategory Validation

    [Fact]
    public void Validate_WithChangeCategoryDefault_DoesNotThrow()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Fact]
    public void Validate_WithChangeCategoryTrueAndDeletePrivateFromClientFalse_DoesNotThrow()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            ChangeCategory = true,
            DeletePrivateTorrentsFromClient = false,
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Fact]
    public void Validate_WithChangeCategoryFalseAndDeletePrivateFromClientTrue_DoesNotThrow()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            ChangeCategory = false,
            DeletePrivateTorrentsFromClient = true,
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Fact]
    public void Validate_WithChangeCategoryAndDeletePrivateFromClientBothTrue_ThrowsValidationException()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            ChangeCategory = true,
            DeletePrivateTorrentsFromClient = true,
        };

        var exception = Should.Throw<CustomValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Cannot enable both deletion and category changing");
    }

    #endregion
}
