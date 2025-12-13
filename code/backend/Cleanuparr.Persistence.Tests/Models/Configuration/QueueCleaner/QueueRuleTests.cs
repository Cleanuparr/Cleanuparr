using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

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

        var exception = Should.Throw<ValidationException>(() => rule.Validate());
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

        var exception = Should.Throw<ValidationException>(() => rule.Validate());
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

        var exception = Should.Throw<ValidationException>(() => rule.Validate());
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

        var exception = Should.Throw<ValidationException>(() => rule.Validate());
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

        var exception = Should.Throw<ValidationException>(() => rule.Validate());
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

        var exception = Should.Throw<ValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Maximum completion percentage must be between 0 and 100");
    }

    [Theory]
    [InlineData((ushort)0)]
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

        var exception = Should.Throw<ValidationException>(() => rule.Validate());
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
}
