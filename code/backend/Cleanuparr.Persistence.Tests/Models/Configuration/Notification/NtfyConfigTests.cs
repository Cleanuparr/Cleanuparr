using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.Notification;

public sealed class NtfyConfigTests
{
    #region IsValid Tests

    [Fact]
    public void IsValid_WithValidUrlAndTopics_ReturnsTrue()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.None
        };

        config.IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyOrNullServerUrl_ReturnsFalse(string? serverUrl)
    {
        var config = new NtfyConfig
        {
            ServerUrl = serverUrl ?? string.Empty,
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.None
        };

        config.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithInvalidServerUrl_ReturnsFalse()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "not-a-valid-url",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.None
        };

        config.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithEmptyTopicsList_ReturnsFalse()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = [],
            AuthenticationType = NtfyAuthenticationType.None
        };

        config.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithOnlyWhitespaceTopics_ReturnsFalse()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["", "   "],
            AuthenticationType = NtfyAuthenticationType.None
        };

        config.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithMixedValidAndEmptyTopics_ReturnsTrue()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["", "valid-topic", "   "],
            AuthenticationType = NtfyAuthenticationType.None
        };

        config.IsValid().ShouldBeTrue();
    }

    #endregion

    #region IsValid Authentication Tests

    [Fact]
    public void IsValid_WithBasicAuth_ValidCredentials_ReturnsTrue()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.BasicAuth,
            Username = "user",
            Password = "pass"
        };

        config.IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, "pass")]
    [InlineData("", "pass")]
    [InlineData("   ", "pass")]
    [InlineData("user", null)]
    [InlineData("user", "")]
    [InlineData("user", "   ")]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void IsValid_WithBasicAuth_InvalidCredentials_ReturnsFalse(string? username, string? password)
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.BasicAuth,
            Username = username,
            Password = password
        };

        config.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithAccessToken_ValidToken_ReturnsTrue()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.AccessToken,
            AccessToken = "tk_valid_token"
        };

        config.IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithAccessToken_InvalidToken_ReturnsFalse(string? accessToken)
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.AccessToken,
            AccessToken = accessToken
        };

        config.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithNoAuth_IgnoresCredentials_ReturnsTrue()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.None,
            Username = null,
            Password = null,
            AccessToken = null
        };

        config.IsValid().ShouldBeTrue();
    }

    #endregion

    #region Uri Property Tests

    [Fact]
    public void Uri_WithValidUrl_ReturnsUri()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh/my-topic"
        };

        config.Uri.ShouldNotBeNull();
        config.Uri.ToString().ShouldBe("https://ntfy.sh/my-topic");
    }

    [Fact]
    public void Uri_WithInvalidUrl_ReturnsNull()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "not-a-url"
        };

        config.Uri.ShouldBeNull();
    }

    [Fact]
    public void Uri_WithEmptyUrl_ReturnsNull()
    {
        var config = new NtfyConfig
        {
            ServerUrl = string.Empty
        };

        config.Uri.ShouldBeNull();
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WithValidConfig_DoesNotThrow()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.None
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrNullServerUrl_ThrowsValidationException(string? serverUrl)
    {
        var config = new NtfyConfig
        {
            ServerUrl = serverUrl ?? string.Empty,
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.None
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("ntfy server URL is required");
    }

    [Fact]
    public void Validate_WithInvalidServerUrl_ThrowsValidationException()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "not-a-valid-url",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.None
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("ntfy server URL must be a valid HTTP or HTTPS URL");
    }

    [Fact]
    public void Validate_WithEmptyTopicsList_ThrowsValidationException()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = [],
            AuthenticationType = NtfyAuthenticationType.None
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("At least one ntfy topic is required");
    }

    [Fact]
    public void Validate_WithOnlyWhitespaceTopics_ThrowsValidationException()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["", "   "],
            AuthenticationType = NtfyAuthenticationType.None
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("At least one ntfy topic is required");
    }

    #endregion

    #region Validate Authentication Tests

    [Fact]
    public void Validate_WithBasicAuth_ValidCredentials_DoesNotThrow()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.BasicAuth,
            Username = "user",
            Password = "pass"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithBasicAuth_MissingUsername_ThrowsValidationException(string? username)
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.BasicAuth,
            Username = username,
            Password = "pass"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Username is required for Basic Auth");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithBasicAuth_MissingPassword_ThrowsValidationException(string? password)
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.BasicAuth,
            Username = "user",
            Password = password
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Password is required for Basic Auth");
    }

    [Fact]
    public void Validate_WithAccessToken_ValidToken_DoesNotThrow()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.AccessToken,
            AccessToken = "tk_valid_token"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithAccessToken_MissingToken_ThrowsValidationException(string? accessToken)
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.AccessToken,
            AccessToken = accessToken
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Access token is required for Token authentication");
    }

    [Fact]
    public void Validate_WithNoAuth_DoesNotRequireCredentials()
    {
        var config = new NtfyConfig
        {
            ServerUrl = "https://ntfy.sh",
            Topics = ["my-topic"],
            AuthenticationType = NtfyAuthenticationType.None,
            Username = null,
            Password = null,
            AccessToken = null
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion
}
