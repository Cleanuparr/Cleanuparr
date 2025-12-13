using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.Notification;

public sealed class PushoverConfigTests
{
    #region IsValid Tests

    [Fact]
    public void IsValid_WithValidApiTokenAndUserKey_ReturnsTrue()
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal
        };

        config.IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyOrNullApiToken_ReturnsFalse(string? apiToken)
    {
        var config = new PushoverConfig
        {
            ApiToken = apiToken ?? string.Empty,
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal
        };

        config.IsValid().ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyOrNullUserKey_ReturnsFalse(string? userKey)
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = userKey ?? string.Empty,
            Priority = PushoverPriority.Normal
        };

        config.IsValid().ShouldBeFalse();
    }

    #endregion

    #region IsValid Emergency Priority Tests

    [Fact]
    public void IsValid_WithEmergencyPriority_ValidRetryAndExpire_ReturnsTrue()
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Emergency,
            Retry = 30,
            Expire = 3600
        };

        config.IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(29)]
    [InlineData(0)]
    [InlineData(-1)]
    public void IsValid_WithEmergencyPriority_InvalidRetry_ReturnsFalse(int? retry)
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Emergency,
            Retry = retry,
            Expire = 3600
        };

        config.IsValid().ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10801)]
    public void IsValid_WithEmergencyPriority_InvalidExpire_ReturnsFalse(int? expire)
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Emergency,
            Retry = 30,
            Expire = expire
        };

        config.IsValid().ShouldBeFalse();
    }

    [Theory]
    [InlineData(PushoverPriority.Lowest)]
    [InlineData(PushoverPriority.Low)]
    [InlineData(PushoverPriority.Normal)]
    [InlineData(PushoverPriority.High)]
    public void IsValid_WithNonEmergencyPriority_DoesNotRequireRetryAndExpire(PushoverPriority priority)
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = priority,
            Retry = null,
            Expire = null
        };

        config.IsValid().ShouldBeTrue();
    }

    #endregion

    #region IsValid Sound Tests

    [Theory]
    [InlineData(null)]
    [InlineData("pushover")]
    [InlineData("bike")]
    [InlineData("custom-sound")]
    public void IsValid_WithValidOrNullSound_ReturnsTrue(string? sound)
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal,
            Sound = sound
        };

        config.IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void IsValid_WithWhitespaceOnlySound_ReturnsFalse(string sound)
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal,
            Sound = sound
        };

        config.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithEmptyStringSound_ReturnsTrue()
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal,
            Sound = string.Empty
        };

        // Empty string has Length 0, so Sound.Length > 0 is false - the condition is skipped
        config.IsValid().ShouldBeTrue();
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WithValidConfig_DoesNotThrow()
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrNullApiToken_ThrowsValidationException(string? apiToken)
    {
        var config = new PushoverConfig
        {
            ApiToken = apiToken ?? string.Empty,
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Pushover API token is required");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrNullUserKey_ThrowsValidationException(string? userKey)
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = userKey ?? string.Empty,
            Priority = PushoverPriority.Normal
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Pushover user key is required");
    }

    #endregion

    #region Validate Emergency Priority Tests

    [Fact]
    public void Validate_WithEmergencyPriority_ValidRetryAndExpire_DoesNotThrow()
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Emergency,
            Retry = 30,
            Expire = 3600
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithEmergencyPriority_MinimumValidRetry_DoesNotThrow()
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Emergency,
            Retry = 30,
            Expire = 1
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithEmergencyPriority_MaximumValidExpire_DoesNotThrow()
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Emergency,
            Retry = 30,
            Expire = 10800
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(29)]
    [InlineData(0)]
    public void Validate_WithEmergencyPriority_RetryTooLowOrNull_ThrowsValidationException(int? retry)
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Emergency,
            Retry = retry,
            Expire = 3600
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Retry interval must be at least 30 seconds for emergency priority");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Validate_WithEmergencyPriority_ExpireNullOrZero_ThrowsValidationException(int? expire)
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Emergency,
            Retry = 30,
            Expire = expire
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Expire time is required for emergency priority");
    }

    [Fact]
    public void Validate_WithEmergencyPriority_ExpireTooHigh_ThrowsValidationException()
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Emergency,
            Retry = 30,
            Expire = 10801
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Expire time cannot exceed 10800 seconds (3 hours)");
    }

    #endregion

    #region Validate Device Tests

    [Fact]
    public void Validate_WithValidDeviceNames_DoesNotThrow()
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal,
            Devices = ["iphone", "android-phone", "my_device"]
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithEmptyDevicesList_DoesNotThrow()
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal,
            Devices = []
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithDeviceNameTooLong_ThrowsValidationException()
    {
        var longDeviceName = new string('a', 26);
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal,
            Devices = [longDeviceName]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe($"Device name '{longDeviceName}' exceeds 25 character limit");
    }

    [Fact]
    public void Validate_WithDeviceNameAtMaxLength_DoesNotThrow()
    {
        var maxLengthDeviceName = new string('a', 25);
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal,
            Devices = [maxLengthDeviceName]
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData("device@name")]
    [InlineData("device name")]
    [InlineData("device.name")]
    [InlineData("device!name")]
    public void Validate_WithDeviceNameInvalidCharacters_ThrowsValidationException(string deviceName)
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal,
            Devices = [deviceName]
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe($"Device name '{deviceName}' contains invalid characters. Only letters, numbers, underscores, and hyphens are allowed.");
    }

    [Theory]
    [InlineData("device-name")]
    [InlineData("device_name")]
    [InlineData("DeviceName123")]
    [InlineData("DEVICE")]
    [InlineData("a")]
    public void Validate_WithDeviceNameValidCharacters_DoesNotThrow(string deviceName)
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal,
            Devices = [deviceName]
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithEmptyOrWhitespaceDeviceNames_SkipsThem()
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal,
            Devices = ["", "   ", "valid-device"]
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate Sound Tests

    [Theory]
    [InlineData(null)]
    [InlineData("pushover")]
    [InlineData("bike")]
    [InlineData("custom-sound")]
    public void Validate_WithValidOrNullSound_DoesNotThrow(string? sound)
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal,
            Sound = sound
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Validate_WithWhitespaceOnlySound_ThrowsValidationException(string sound)
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal,
            Sound = sound
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Sound name cannot be empty or whitespace when specified");
    }

    [Fact]
    public void Validate_WithEmptyStringSound_DoesNotThrow()
    {
        var config = new PushoverConfig
        {
            ApiToken = "test-api-token-1234567890",
            UserKey = "test-user-key-1234567890",
            Priority = PushoverPriority.Normal,
            Sound = string.Empty
        };

        // Empty string has Length 0, so Sound.Length > 0 is false - the condition is skipped
        Should.NotThrow(() => config.Validate());
    }

    #endregion
}
