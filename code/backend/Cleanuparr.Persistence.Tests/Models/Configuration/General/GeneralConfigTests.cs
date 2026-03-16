using Cleanuparr.Persistence.Models.Configuration.General;
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

    #endregion

    #region Validate - HttpTimeout Validation

    [Fact]
    public void Validate_WithZeroHttpTimeout_ThrowsValidationException()
    {
        var config = new GeneralConfig
        {
            HttpTimeout = 0,
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
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Calls LoggingConfig.Validate

    [Fact]
    public void Validate_WithInvalidLoggingConfig_ThrowsValidationException()
    {
        var config = new GeneralConfig
        {
            HttpTimeout = 100,
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
