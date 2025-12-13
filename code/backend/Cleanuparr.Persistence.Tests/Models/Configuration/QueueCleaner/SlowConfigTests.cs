using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.QueueCleaner;

public sealed class SlowConfigTests
{
    #region Validate - Valid Configurations

    [Fact]
    public void Validate_WithDisabledConfig_DoesNotThrow()
    {
        var config = new SlowConfig
        {
            MaxStrikes = 0
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithValidMinSpeed_DoesNotThrow()
    {
        var config = new SlowConfig
        {
            MaxStrikes = 3,
            MinSpeed = "100KB",
            MaxTime = 0
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithValidMaxTime_DoesNotThrow()
    {
        var config = new SlowConfig
        {
            MaxStrikes = 3,
            MinSpeed = "",
            MaxTime = 24
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithBothMinSpeedAndMaxTime_DoesNotThrow()
    {
        var config = new SlowConfig
        {
            MaxStrikes = 3,
            MinSpeed = "1MB",
            MaxTime = 48
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - MaxStrikes Validation

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)2)]
    public void Validate_WithMaxStrikesBetween1And2_ThrowsValidationException(ushort maxStrikes)
    {
        var config = new SlowConfig
        {
            MaxStrikes = maxStrikes,
            MinSpeed = "100KB",
            MaxTime = 0
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("the minimum value for slow max strikes must be 3");
    }

    [Fact]
    public void Validate_WithMinimumValidMaxStrikes_DoesNotThrow()
    {
        var config = new SlowConfig
        {
            MaxStrikes = 3,
            MinSpeed = "100KB",
            MaxTime = 0
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - MinSpeed Validation

    [Theory]
    [InlineData("invalid")]
    [InlineData("abc")]
    [InlineData("100")]
    [InlineData("KB")]
    public void Validate_WithInvalidMinSpeedFormat_ThrowsValidationException(string minSpeed)
    {
        var config = new SlowConfig
        {
            MaxStrikes = 3,
            MinSpeed = minSpeed,
            MaxTime = 0
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("invalid value for slow min speed");
    }

    [Theory]
    [InlineData("1KB")]
    [InlineData("100KB")]
    [InlineData("1MB")]
    [InlineData("1GB")]
    public void Validate_WithValidMinSpeedFormats_DoesNotThrow(string minSpeed)
    {
        var config = new SlowConfig
        {
            MaxStrikes = 3,
            MinSpeed = minSpeed,
            MaxTime = 0
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - MaxTime Validation

    [Fact]
    public void Validate_WithNegativeMaxTime_ThrowsValidationException()
    {
        var config = new SlowConfig
        {
            MaxStrikes = 3,
            MinSpeed = "100KB",
            MaxTime = -1
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("invalid value for slow max time");
    }

    #endregion

    #region Validate - MinSpeed and MaxTime Required

    [Fact]
    public void Validate_WithNoMinSpeedAndNoMaxTime_ThrowsValidationException()
    {
        var config = new SlowConfig
        {
            MaxStrikes = 3,
            MinSpeed = "",
            MaxTime = 0
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("either slow min speed or slow max time must be set");
    }

    #endregion

    #region Validate - IgnoreAboveSize Validation

    [Theory]
    [InlineData("100MB")]
    [InlineData("1GB")]
    [InlineData("10GB")]
    public void Validate_WithValidIgnoreAboveSize_DoesNotThrow(string ignoreAboveSize)
    {
        var config = new SlowConfig
        {
            MaxStrikes = 3,
            MinSpeed = "100KB",
            MaxTime = 0,
            IgnoreAboveSize = ignoreAboveSize
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithEmptyIgnoreAboveSize_DoesNotThrow()
    {
        var config = new SlowConfig
        {
            MaxStrikes = 3,
            MinSpeed = "100KB",
            MaxTime = 0,
            IgnoreAboveSize = ""
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("abc")]
    [InlineData("100")]
    public void Validate_WithInvalidIgnoreAboveSizeFormat_ThrowsValidationException(string ignoreAboveSize)
    {
        var config = new SlowConfig
        {
            MaxStrikes = 3,
            MinSpeed = "100KB",
            MaxTime = 0,
            IgnoreAboveSize = ignoreAboveSize
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldContain("invalid value for slow ignore above size");
    }

    #endregion

    #region ByteSize Property Tests

    [Fact]
    public void MinSpeedByteSize_WithValidSpeed_ParsesCorrectly()
    {
        var config = new SlowConfig
        {
            MinSpeed = "1MB"
        };

        config.MinSpeedByteSize.Bytes.ShouldBe(1024 * 1024);
    }

    [Fact]
    public void MinSpeedByteSize_WithEmptySpeed_ReturnsZero()
    {
        var config = new SlowConfig
        {
            MinSpeed = ""
        };

        config.MinSpeedByteSize.Bytes.ShouldBe(0);
    }

    [Fact]
    public void IgnoreAboveSizeByteSize_WithValidSize_ParsesCorrectly()
    {
        var config = new SlowConfig
        {
            MinSpeed = "100KB",
            IgnoreAboveSize = "1GB"
        };

        config.IgnoreAboveSizeByteSize.ShouldNotBeNull();
        config.IgnoreAboveSizeByteSize!.Value.Bytes.ShouldBe(1024L * 1024 * 1024);
    }

    [Fact]
    public void IgnoreAboveSizeByteSize_WithEmptySize_ReturnsNull()
    {
        var config = new SlowConfig
        {
            MinSpeed = "100KB",
            IgnoreAboveSize = ""
        };

        config.IgnoreAboveSizeByteSize.ShouldBeNull();
    }

    #endregion
}
