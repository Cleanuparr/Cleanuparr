using Cleanuparr.Persistence.Models.Configuration.General;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.General;

public sealed class LoggingConfigTests
{
    #region Validate - Valid Configurations

    [Fact]
    public void Validate_WithDefaultConfig_DoesNotThrow()
    {
        var config = new LoggingConfig();

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithAllMaxValues_DoesNotThrow()
    {
        var config = new LoggingConfig
        {
            RollingSizeMB = 100,
            RetainedFileCount = 50,
            TimeLimitHours = 1440,
            ArchiveEnabled = true,
            ArchiveRetainedCount = 100,
            ArchiveTimeLimitHours = 1440
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithArchiveDisabled_DoesNotRequireRetentionPolicy()
    {
        var config = new LoggingConfig
        {
            ArchiveEnabled = false,
            ArchiveRetainedCount = 0,
            ArchiveTimeLimitHours = 0
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - RollingSizeMB Validation

    [Fact]
    public void Validate_WithRollingSizeExceedingMax_ThrowsValidationException()
    {
        var config = new LoggingConfig
        {
            RollingSizeMB = 101
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Log rolling size cannot exceed 100 MB");
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    [InlineData((ushort)50)]
    [InlineData((ushort)100)]
    public void Validate_WithValidRollingSize_DoesNotThrow(ushort rollingSizeMB)
    {
        var config = new LoggingConfig
        {
            RollingSizeMB = rollingSizeMB,
            ArchiveEnabled = false
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - RetainedFileCount Validation

    [Fact]
    public void Validate_WithRetainedFileCountExceedingMax_ThrowsValidationException()
    {
        var config = new LoggingConfig
        {
            RetainedFileCount = 51
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Log retained file count cannot exceed 50");
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    [InlineData((ushort)25)]
    [InlineData((ushort)50)]
    public void Validate_WithValidRetainedFileCount_DoesNotThrow(ushort retainedFileCount)
    {
        var config = new LoggingConfig
        {
            RetainedFileCount = retainedFileCount,
            ArchiveEnabled = false
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - TimeLimitHours Validation

    [Fact]
    public void Validate_WithTimeLimitExceedingMax_ThrowsValidationException()
    {
        var config = new LoggingConfig
        {
            TimeLimitHours = 1441
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Log time limit cannot exceed 60 days");
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)24)]
    [InlineData((ushort)720)]
    [InlineData((ushort)1440)]
    public void Validate_WithValidTimeLimitHours_DoesNotThrow(ushort timeLimitHours)
    {
        var config = new LoggingConfig
        {
            TimeLimitHours = timeLimitHours,
            ArchiveEnabled = false
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - ArchiveRetainedCount Validation

    [Fact]
    public void Validate_WithArchiveRetainedCountExceedingMax_ThrowsValidationException()
    {
        var config = new LoggingConfig
        {
            ArchiveRetainedCount = 101
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Log archive retained count cannot exceed 100");
    }

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)50)]
    [InlineData((ushort)100)]
    public void Validate_WithValidArchiveRetainedCount_DoesNotThrow(ushort archiveRetainedCount)
    {
        var config = new LoggingConfig
        {
            ArchiveEnabled = true,
            ArchiveRetainedCount = archiveRetainedCount,
            ArchiveTimeLimitHours = 0
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - ArchiveTimeLimitHours Validation

    [Fact]
    public void Validate_WithArchiveTimeLimitExceedingMax_ThrowsValidationException()
    {
        var config = new LoggingConfig
        {
            ArchiveTimeLimitHours = 1441
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Log archive time limit cannot exceed 60 days");
    }

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)720)]
    [InlineData((ushort)1440)]
    public void Validate_WithValidArchiveTimeLimitHours_DoesNotThrow(ushort archiveTimeLimitHours)
    {
        var config = new LoggingConfig
        {
            ArchiveEnabled = true,
            ArchiveRetainedCount = 0,
            ArchiveTimeLimitHours = archiveTimeLimitHours
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Archive Retention Policy Validation

    [Fact]
    public void Validate_WithArchiveEnabledAndNoRetentionPolicy_ThrowsValidationException()
    {
        var config = new LoggingConfig
        {
            ArchiveEnabled = true,
            ArchiveRetainedCount = 0,
            ArchiveTimeLimitHours = 0
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Archiving is enabled, but no retention policy is set. Please set either a retained file count or time limit");
    }

    [Fact]
    public void Validate_WithArchiveEnabledAndOnlyRetainedCount_DoesNotThrow()
    {
        var config = new LoggingConfig
        {
            ArchiveEnabled = true,
            ArchiveRetainedCount = 10,
            ArchiveTimeLimitHours = 0
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithArchiveEnabledAndOnlyTimeLimitHours_DoesNotThrow()
    {
        var config = new LoggingConfig
        {
            ArchiveEnabled = true,
            ArchiveRetainedCount = 0,
            ArchiveTimeLimitHours = 720
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithArchiveEnabledAndBothRetentionPolicies_DoesNotThrow()
    {
        var config = new LoggingConfig
        {
            ArchiveEnabled = true,
            ArchiveRetainedCount = 10,
            ArchiveTimeLimitHours = 720
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion
}
