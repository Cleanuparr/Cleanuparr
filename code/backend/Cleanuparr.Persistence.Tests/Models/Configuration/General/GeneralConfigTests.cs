using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Shared.Helpers;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.General;

public sealed class GeneralConfigTests
{
    #region Validate - Valid Configurations

    [Fact]
    public void Validate_WithDefaultConfig_DoesNotThrow()
    {
        var config = new GeneralConfig();

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithMinimumSearchDelay_DoesNotThrow()
    {
        var config = new GeneralConfig
        {
            HttpTimeout = 100,
            SearchDelay = (ushort)Constants.MinSearchDelaySeconds
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithAboveMinimumSearchDelay_DoesNotThrow()
    {
        var config = new GeneralConfig
        {
            HttpTimeout = 100,
            SearchDelay = 300
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - HttpTimeout Validation

    [Fact]
    public void Validate_WithZeroHttpTimeout_ThrowsValidationException()
    {
        var config = new GeneralConfig
        {
            HttpTimeout = 0,
            SearchDelay = (ushort)Constants.MinSearchDelaySeconds
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("HttpTimeout must be greater than 0");
    }

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)50)]
    [InlineData((ushort)100)]
    [InlineData((ushort)65535)]
    public void Validate_WithPositiveHttpTimeout_DoesNotThrow(ushort httpTimeout)
    {
        var config = new GeneralConfig
        {
            HttpTimeout = httpTimeout,
            SearchDelay = (ushort)Constants.MinSearchDelaySeconds
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - SearchDelay Validation

    [Fact]
    public void Validate_WithBelowMinimumSearchDelay_ThrowsValidationException()
    {
        var config = new GeneralConfig
        {
            HttpTimeout = 100,
            SearchDelay = (ushort)(Constants.MinSearchDelaySeconds - 1)
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe($"SearchDelay must be at least {Constants.MinSearchDelaySeconds} seconds");
    }

    [Fact]
    public void Validate_WithZeroSearchDelay_ThrowsValidationException()
    {
        var config = new GeneralConfig
        {
            HttpTimeout = 100,
            SearchDelay = 0
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe($"SearchDelay must be at least {Constants.MinSearchDelaySeconds} seconds");
    }

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)30)]
    [InlineData((ushort)59)]
    public void Validate_WithVariousBelowMinimumSearchDelay_ThrowsValidationException(ushort searchDelay)
    {
        var config = new GeneralConfig
        {
            HttpTimeout = 100,
            SearchDelay = searchDelay
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe($"SearchDelay must be at least {Constants.MinSearchDelaySeconds} seconds");
    }

    #endregion

    #region Validate - Calls LoggingConfig.Validate

    [Fact]
    public void Validate_WithInvalidLoggingConfig_ThrowsValidationException()
    {
        var config = new GeneralConfig
        {
            HttpTimeout = 100,
            SearchDelay = (ushort)Constants.MinSearchDelaySeconds,
            Log = new LoggingConfig
            {
                RollingSizeMB = 101 // Exceeds max of 100
            }
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Log rolling size cannot exceed 100 MB");
    }

    #endregion
}
