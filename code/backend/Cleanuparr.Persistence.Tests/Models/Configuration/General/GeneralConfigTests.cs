using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration.General;
using Serilog;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.General;

public class GeneralConfigTests
{
    [Fact]
    public void GeneralConfig_DefaultValues_ShouldBeValid()
    {
        // Arrange
        var config = new GeneralConfig();

        // Act & Assert - Should not throw
        config.Validate();
    }

    [Fact]
    public void GeneralConfig_DefaultValues_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var config = new GeneralConfig();

        // Assert
        Assert.Equal(RollingInterval.Day, config.LogRollingInterval);
        Assert.Equal((ushort)10, config.LogRollingSizeMB);
        Assert.Equal((ushort)5, config.LogRetainedFileCount);
        Assert.True(config.LogArchiveEnabled);
        Assert.Equal((ushort)30, config.LogArchiveRetainedCount);
        Assert.Equal((ushort)30, config.LogArchiveTimeLimitDays);
    }

    [Theory]
    [InlineData(RollingInterval.Hour)]
    [InlineData(RollingInterval.Day)]
    public void Validate_ValidRollingInterval_ShouldNotThrow(RollingInterval interval)
    {
        // Arrange
        var config = new GeneralConfig { LogRollingInterval = interval };

        // Act & Assert - Should not throw
        config.Validate();
    }

    [Theory]
    [InlineData(RollingInterval.Month)]
    [InlineData(RollingInterval.Year)]
    [InlineData(RollingInterval.Minute)]
    public void Validate_InvalidRollingInterval_ShouldThrow(RollingInterval interval)
    {
        // Arrange
        var config = new GeneralConfig { LogRollingInterval = interval };

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => config.Validate());
        Assert.Contains("Log rolling interval must be either Hour or Day", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_ValidRollingSizeMB_ShouldNotThrow(ushort size)
    {
        // Arrange
        var config = new GeneralConfig { LogRollingSizeMB = size };

        // Act & Assert - Should not throw
        config.Validate();
    }

    [Fact]
    public void Validate_InvalidRollingSizeMB_ShouldThrow()
    {
        // Arrange
        var config = new GeneralConfig { LogRollingSizeMB = 101 };

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => config.Validate());
        Assert.Contains("Log rolling size cannot exceed 100 MB", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(50)]
    public void Validate_ValidRetainedFileCount_ShouldNotThrow(ushort count)
    {
        // Arrange
        var config = new GeneralConfig { LogRetainedFileCount = count };

        // Act & Assert - Should not throw
        config.Validate();
    }

    [Fact]
    public void Validate_InvalidRetainedFileCount_ShouldThrow()
    {
        // Arrange
        var config = new GeneralConfig { LogRetainedFileCount = 51 };

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => config.Validate());
        Assert.Contains("Log retained file count cannot exceed 50", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_ValidArchiveRetainedCount_ShouldNotThrow(ushort count)
    {
        // Arrange
        var config = new GeneralConfig { LogArchiveRetainedCount = count };

        // Act & Assert - Should not throw
        config.Validate();
    }

    [Fact]
    public void Validate_InvalidArchiveRetainedCount_ShouldThrow()
    {
        // Arrange
        var config = new GeneralConfig { LogArchiveRetainedCount = 101 };

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => config.Validate());
        Assert.Contains("Log archive retained count cannot exceed 100", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(60)]
    public void Validate_ValidArchiveTimeLimitDays_ShouldNotThrow(ushort days)
    {
        // Arrange
        var config = new GeneralConfig { LogArchiveTimeLimitDays = days };

        // Act & Assert - Should not throw
        config.Validate();
    }

    [Fact]
    public void Validate_InvalidArchiveTimeLimitDays_ShouldThrow()
    {
        // Arrange
        var config = new GeneralConfig { LogArchiveTimeLimitDays = 61 };

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => config.Validate());
        Assert.Contains("Log archive time limit cannot exceed 60 days", exception.Message);
    }
}