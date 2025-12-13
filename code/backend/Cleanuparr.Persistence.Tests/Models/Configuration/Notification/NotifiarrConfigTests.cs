using Cleanuparr.Persistence.Models.Configuration.Notification;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.Notification;

public sealed class NotifiarrConfigTests
{
    #region IsValid Tests

    [Fact]
    public void IsValid_WithValidApiKeyAndChannelId_ReturnsTrue()
    {
        var config = new NotifiarrConfig
        {
            ApiKey = "valid-api-key-12345",
            ChannelId = "123456789012345678"
        };

        config.IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyOrNullApiKey_ReturnsFalse(string? apiKey)
    {
        var config = new NotifiarrConfig
        {
            ApiKey = apiKey ?? string.Empty,
            ChannelId = "123456789012345678"
        };

        config.IsValid().ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyOrNullChannelId_ReturnsFalse(string? channelId)
    {
        var config = new NotifiarrConfig
        {
            ApiKey = "valid-api-key-12345",
            ChannelId = channelId ?? string.Empty
        };

        config.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithBothFieldsEmpty_ReturnsFalse()
    {
        var config = new NotifiarrConfig
        {
            ApiKey = string.Empty,
            ChannelId = string.Empty
        };

        config.IsValid().ShouldBeFalse();
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WithValidConfig_DoesNotThrow()
    {
        var config = new NotifiarrConfig
        {
            ApiKey = "valid-api-key-12345",
            ChannelId = "123456789012345678"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrNullApiKey_ThrowsValidationException(string? apiKey)
    {
        var config = new NotifiarrConfig
        {
            ApiKey = apiKey ?? string.Empty,
            ChannelId = "123456789012345678"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Notifiarr API key is required");
    }

    [Fact]
    public void Validate_WithShortApiKey_ThrowsValidationException()
    {
        var config = new NotifiarrConfig
        {
            ApiKey = "short",
            ChannelId = "123456789012345678"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Notifiarr API key must be at least 10 characters long");
    }

    [Fact]
    public void Validate_WithMinimumLengthApiKey_DoesNotThrow()
    {
        var config = new NotifiarrConfig
        {
            ApiKey = "1234567890",
            ChannelId = "123456789012345678"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrNullChannelId_ThrowsValidationException(string? channelId)
    {
        var config = new NotifiarrConfig
        {
            ApiKey = "valid-api-key-12345",
            ChannelId = channelId ?? string.Empty
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Discord channel ID is required");
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("abc123")]
    [InlineData("12.34")]
    [InlineData("-123")]
    public void Validate_WithNonNumericChannelId_ThrowsValidationException(string channelId)
    {
        var config = new NotifiarrConfig
        {
            ApiKey = "valid-api-key-12345",
            ChannelId = channelId
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Discord channel ID must be a valid numeric ID");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("123456789012345678")]
    [InlineData("18446744073709551615")]
    public void Validate_WithValidNumericChannelId_DoesNotThrow(string channelId)
    {
        var config = new NotifiarrConfig
        {
            ApiKey = "valid-api-key-12345",
            ChannelId = channelId
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion
}
