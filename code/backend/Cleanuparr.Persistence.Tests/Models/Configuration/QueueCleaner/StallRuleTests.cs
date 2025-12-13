using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.QueueCleaner;

public sealed class StallRuleTests
{
    #region Validate - Valid Configurations

    [Fact]
    public void Validate_WithValidConfig_DoesNotThrow()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Fact]
    public void Validate_WithValidMinimumProgress_DoesNotThrow()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinimumProgress = "1MB"
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Fact]
    public void Validate_WithNullMinimumProgress_DoesNotThrow()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinimumProgress = null
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Fact]
    public void Validate_WithEmptyMinimumProgress_DoesNotThrow()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinimumProgress = ""
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Fact]
    public void Validate_WithWhitespaceMinimumProgress_DoesNotThrow()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinimumProgress = "   "
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Theory]
    [InlineData("1KB")]
    [InlineData("100KB")]
    [InlineData("1MB")]
    [InlineData("10MB")]
    [InlineData("1GB")]
    public void Validate_WithVariousValidMinimumProgressFormats_DoesNotThrow(string minimumProgress)
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinimumProgress = minimumProgress
        };

        Should.NotThrow(() => rule.Validate());
    }

    #endregion

    #region Validate - MaxStrikes Validation (Override)

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Validate_WithMaxStrikesLessThan3_ThrowsValidationException(int maxStrikes)
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = maxStrikes
        };

        // Base class validation runs first
        var exception = Should.Throw<ValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Max strikes must be at least 3");
    }

    #endregion

    #region Validate - MinimumProgress Validation

    [Theory]
    [InlineData("invalid")]
    [InlineData("abc")]
    [InlineData("100")]
    [InlineData("KB")]
    public void Validate_WithInvalidMinimumProgressFormat_ThrowsValidationException(string minimumProgress)
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinimumProgress = minimumProgress
        };

        var exception = Should.Throw<ValidationException>(() => rule.Validate());
        exception.Message.ShouldContain("Invalid minimum progress value");
    }

    #endregion

    #region ByteSize Property Tests

    [Fact]
    public void MinimumProgressByteSize_WithValidProgress_ParsesCorrectly()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinimumProgress = "1MB"
        };

        rule.MinimumProgressByteSize.ShouldNotBeNull();
        rule.MinimumProgressByteSize!.Value.Bytes.ShouldBe(1024 * 1024);
    }

    [Fact]
    public void MinimumProgressByteSize_WithNullProgress_ReturnsNull()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinimumProgress = null
        };

        rule.MinimumProgressByteSize.ShouldBeNull();
    }

    [Fact]
    public void MinimumProgressByteSize_WithEmptyProgress_ReturnsNull()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinimumProgress = ""
        };

        rule.MinimumProgressByteSize.ShouldBeNull();
    }

    [Fact]
    public void MinimumProgressByteSize_WithWhitespaceProgress_ReturnsNull()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinimumProgress = "   "
        };

        rule.MinimumProgressByteSize.ShouldBeNull();
    }

    [Theory]
    [InlineData("1KB", 1024)]
    [InlineData("1MB", 1024 * 1024)]
    [InlineData("1GB", 1024L * 1024 * 1024)]
    public void MinimumProgressByteSize_WithDifferentUnits_ParsesCorrectly(string minimumProgress, long expectedBytes)
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinimumProgress = minimumProgress
        };

        rule.MinimumProgressByteSize.ShouldNotBeNull();
        rule.MinimumProgressByteSize!.Value.Bytes.ShouldBe(expectedBytes);
    }

    #endregion

    #region ResetStrikesOnProgress Tests

    [Fact]
    public void ResetStrikesOnProgress_DefaultsToTrue()
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3
        };

        rule.ResetStrikesOnProgress.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResetStrikesOnProgress_CanBeSet(bool resetStrikesOnProgress)
    {
        var rule = new StallRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            ResetStrikesOnProgress = resetStrikesOnProgress
        };

        rule.ResetStrikesOnProgress.ShouldBe(resetStrikesOnProgress);
    }

    #endregion
}
