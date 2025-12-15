using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.Notification;

public sealed class AppriseConfigTests
{
    #region IsValid Tests

    [Fact]
    public void IsValid_WithValidUrlAndKey_ReturnsTrue()
    {
        var config = new AppriseConfig
        {
            Url = "https://apprise.example.com",
            Key = "my-config-key"
        };

        config.IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyOrNullUrl_ReturnsFalse(string? url)
    {
        var config = new AppriseConfig
        {
            Url = url ?? string.Empty,
            Key = "my-config-key"
        };

        config.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithInvalidUrl_ReturnsFalse()
    {
        var config = new AppriseConfig
        {
            Url = "not-a-valid-url",
            Key = "my-config-key"
        };

        config.IsValid().ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyOrNullKey_ReturnsFalse(string? key)
    {
        var config = new AppriseConfig
        {
            Url = "https://apprise.example.com",
            Key = key ?? string.Empty
        };

        config.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithHttpUrl_ReturnsTrue()
    {
        var config = new AppriseConfig
        {
            Url = "http://apprise.local:8080",
            Key = "config-key"
        };

        config.IsValid().ShouldBeTrue();
    }

    #endregion

    #region Uri Property Tests

    [Fact]
    public void Uri_WithValidUrl_ReturnsUri()
    {
        var config = new AppriseConfig
        {
            Url = "https://apprise.example.com/notify"
        };

        config.Uri.ShouldNotBeNull();
        config.Uri.ToString().ShouldBe("https://apprise.example.com/notify");
    }

    [Fact]
    public void Uri_WithInvalidUrl_ReturnsNull()
    {
        var config = new AppriseConfig
        {
            Url = "not-a-url"
        };

        config.Uri.ShouldBeNull();
    }

    [Fact]
    public void Uri_WithEmptyUrl_ReturnsNull()
    {
        var config = new AppriseConfig
        {
            Url = string.Empty
        };

        config.Uri.ShouldBeNull();
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WithValidConfig_DoesNotThrow()
    {
        var config = new AppriseConfig
        {
            Url = "https://apprise.example.com",
            Key = "my-config-key"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrNullUrl_ThrowsValidationException(string? url)
    {
        var config = new AppriseConfig
        {
            Url = url ?? string.Empty,
            Key = "my-config-key"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Apprise server URL is required for API mode");
    }

    [Fact]
    public void Validate_WithInvalidUrl_ThrowsValidationException()
    {
        var config = new AppriseConfig
        {
            Url = "not-a-valid-url",
            Key = "my-config-key"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Apprise server URL must be a valid HTTP or HTTPS URL");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrNullKey_ThrowsValidationException(string? key)
    {
        var config = new AppriseConfig
        {
            Url = "https://apprise.example.com",
            Key = key ?? string.Empty
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Apprise configuration key is required for API mode");
    }

    [Fact]
    public void Validate_WithShortKey_ThrowsValidationException()
    {
        var config = new AppriseConfig
        {
            Url = "https://apprise.example.com",
            Key = "a"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Apprise configuration key must be at least 2 characters long");
    }

    [Fact]
    public void Validate_WithMinimumLengthKey_DoesNotThrow()
    {
        var config = new AppriseConfig
        {
            Url = "https://apprise.example.com",
            Key = "ab"
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region CLI Mode Tests

    [Fact]
    public void IsValid_CliMode_WithValidServiceUrls_ReturnsTrue()
    {
        var config = new AppriseConfig
        {
            Mode = AppriseMode.Cli,
            ServiceUrls = "discord://webhook_id/webhook_token"
        };

        config.IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_CliMode_WithEmptyServiceUrls_ReturnsFalse(string? serviceUrls)
    {
        var config = new AppriseConfig
        {
            Mode = AppriseMode.Cli,
            ServiceUrls = serviceUrls
        };

        config.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void Validate_CliMode_WithValidServiceUrls_DoesNotThrow()
    {
        var config = new AppriseConfig
        {
            Mode = AppriseMode.Cli,
            ServiceUrls = "discord://webhook_id/webhook_token\nslack://token_a/token_b/token_c"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_CliMode_WithEmptyServiceUrls_ThrowsValidationException(string? serviceUrls)
    {
        var config = new AppriseConfig
        {
            Mode = AppriseMode.Cli,
            ServiceUrls = serviceUrls
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("At least one service URL is required for CLI mode");
    }

    [Fact]
    public void Validate_CliMode_WithValidUrlAndWhitespaceLines_DoesNotThrow()
    {
        // url1 is valid content, whitespace lines should be filtered out
        var config = new AppriseConfig
        {
            Mode = AppriseMode.Cli,
            ServiceUrls = "discord://webhook_id/token\n   \n   "
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void IsValid_CliMode_IgnoresApiModeFields()
    {
        var config = new AppriseConfig
        {
            Mode = AppriseMode.Cli,
            Url = string.Empty, // Would be invalid in API mode
            Key = string.Empty, // Would be invalid in API mode
            ServiceUrls = "discord://webhook_id/webhook_token"
        };

        config.IsValid().ShouldBeTrue();
    }

    [Fact]
    public void IsValid_ApiMode_IgnoresCliModeFields()
    {
        var config = new AppriseConfig
        {
            Mode = AppriseMode.Api,
            Url = "https://apprise.example.com",
            Key = "my-key",
            ServiceUrls = null // Would be invalid in CLI mode
        };

        config.IsValid().ShouldBeTrue();
    }

    #endregion
}
