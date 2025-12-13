using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.QueueCleaner;

public sealed class SlowRuleTests
{
    #region Validate - Valid Configurations

    [Fact]
    public void Validate_WithValidMinSpeed_DoesNotThrow()
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = "100KB",
            MaxTimeHours = 0
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Fact]
    public void Validate_WithValidMaxTimeHours_DoesNotThrow()
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = "",
            MaxTimeHours = 24
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Fact]
    public void Validate_WithBothMinSpeedAndMaxTimeHours_DoesNotThrow()
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = "1MB",
            MaxTimeHours = 48
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Theory]
    [InlineData("1KB")]
    [InlineData("100KB")]
    [InlineData("1MB")]
    [InlineData("10MB")]
    [InlineData("1GB")]
    public void Validate_WithVariousValidMinSpeedFormats_DoesNotThrow(string minSpeed)
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = minSpeed,
            MaxTimeHours = 0
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
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = maxStrikes,
            MinSpeed = "100KB",
            MaxTimeHours = 0
        };

        // Base class validation runs first
        var exception = Should.Throw<ValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Max strikes must be at least 3");
    }

    #endregion

    #region Validate - MaxTimeHours Validation

    [Fact]
    public void Validate_WithNegativeMaxTimeHours_ThrowsValidationException()
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = "100KB",
            MaxTimeHours = -1
        };

        var exception = Should.Throw<ValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Maximum time cannot be negative");
    }

    [Fact]
    public void Validate_WithZeroMaxTimeHoursAndValidMinSpeed_DoesNotThrow()
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = "100KB",
            MaxTimeHours = 0
        };

        Should.NotThrow(() => rule.Validate());
    }

    #endregion

    #region Validate - MinSpeed and MaxTime Required

    [Fact]
    public void Validate_WithNoMinSpeedAndNoMaxTime_ThrowsValidationException()
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = "",
            MaxTimeHours = 0
        };

        var exception = Should.Throw<ValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Either minimum speed or maximum time must be specified");
    }

    [Fact]
    public void Validate_WithEmptyMinSpeedAndZeroMaxTime_ThrowsValidationException()
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = string.Empty,
            MaxTimeHours = 0
        };

        var exception = Should.Throw<ValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Either minimum speed or maximum time must be specified");
    }

    #endregion

    #region Validate - MinSpeed Format Validation

    [Theory]
    [InlineData("invalid")]
    [InlineData("abc")]
    [InlineData("100")]
    [InlineData("KB")]
    public void Validate_WithInvalidMinSpeedFormat_ThrowsValidationException(string minSpeed)
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = minSpeed,
            MaxTimeHours = 0
        };

        var exception = Should.Throw<ValidationException>(() => rule.Validate());
        exception.Message.ShouldBe("Invalid minimum speed format");
    }

    #endregion

    #region Validate - IgnoreAboveSize Validation

    [Theory]
    [InlineData("100MB")]
    [InlineData("1GB")]
    [InlineData("10GB")]
    public void Validate_WithValidIgnoreAboveSize_DoesNotThrow(string ignoreAboveSize)
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = "100KB",
            MaxTimeHours = 0,
            IgnoreAboveSize = ignoreAboveSize
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Fact]
    public void Validate_WithEmptyIgnoreAboveSize_DoesNotThrow()
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = "100KB",
            MaxTimeHours = 0,
            IgnoreAboveSize = ""
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Fact]
    public void Validate_WithNullIgnoreAboveSize_DoesNotThrow()
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = "100KB",
            MaxTimeHours = 0,
            IgnoreAboveSize = null
        };

        Should.NotThrow(() => rule.Validate());
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("abc")]
    [InlineData("100")]
    public void Validate_WithInvalidIgnoreAboveSizeFormat_ThrowsValidationException(string ignoreAboveSize)
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = "100KB",
            MaxTimeHours = 0,
            IgnoreAboveSize = ignoreAboveSize
        };

        var exception = Should.Throw<ValidationException>(() => rule.Validate());
        exception.Message.ShouldContain("invalid value for slow ignore above size");
    }

    #endregion

    #region ByteSize Property Tests

    [Fact]
    public void MinSpeedByteSize_WithValidSpeed_ParsesCorrectly()
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = "1MB"
        };

        rule.MinSpeedByteSize.Bytes.ShouldBe(1024 * 1024);
    }

    [Fact]
    public void MinSpeedByteSize_WithEmptySpeed_ReturnsZero()
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = ""
        };

        rule.MinSpeedByteSize.Bytes.ShouldBe(0);
    }

    [Fact]
    public void IgnoreAboveSizeByteSize_WithValidSize_ParsesCorrectly()
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = "100KB",
            IgnoreAboveSize = "1GB"
        };

        rule.IgnoreAboveSizeByteSize.ShouldNotBeNull();
        rule.IgnoreAboveSizeByteSize!.Value.Bytes.ShouldBe(1024L * 1024 * 1024);
    }

    [Fact]
    public void IgnoreAboveSizeByteSize_WithEmptySize_ReturnsNull()
    {
        var rule = new SlowRule
        {
            Name = "test-rule",
            MaxStrikes = 3,
            MinSpeed = "100KB",
            IgnoreAboveSize = ""
        };

        rule.IgnoreAboveSizeByteSize.ShouldBeNull();
    }

    #endregion
}
