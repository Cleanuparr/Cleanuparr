using Cleanuparr.Persistence.Models.Configuration.Notification;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.Notification;

public sealed class GotifyConfigTests
{
    #region IsValid Tests

    [Fact]
    public void IsValid_WithValidConfig_ReturnsTrue()
    {
        var config = new GotifyConfig
        {
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "test-app-token"
        };

        config.IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyOrNullServerUrl_ReturnsFalse(string? serverUrl)
    {
        var config = new GotifyConfig
        {
            ServerUrl = serverUrl ?? string.Empty,
            ApplicationToken = "test-token"
        };

        config.IsValid().ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyOrNullApplicationToken_ReturnsFalse(string? token)
    {
        var config = new GotifyConfig
        {
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = token ?? string.Empty
        };

        config.IsValid().ShouldBeFalse();
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WithValidConfig_DoesNotThrow()
    {
        var config = new GotifyConfig
        {
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "test-app-token",
            Priority = 5
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrNullServerUrl_ThrowsValidationException(string? serverUrl)
    {
        var config = new GotifyConfig
        {
            ServerUrl = serverUrl ?? string.Empty,
            ApplicationToken = "test-token"
        };

        var ex = Should.Throw<ValidationException>(() => config.Validate());
        ex.Message.ShouldContain("required");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://gotify.example.com")]
    [InlineData("invalid://scheme")]
    public void Validate_WithInvalidServerUrl_ThrowsValidationException(string serverUrl)
    {
        var config = new GotifyConfig
        {
            ServerUrl = serverUrl,
            ApplicationToken = "test-token"
        };

        var ex = Should.Throw<ValidationException>(() => config.Validate());
        ex.Message.ShouldContain("valid HTTP or HTTPS URL");
    }

    [Theory]
    [InlineData("https://gotify.example.com")]
    [InlineData("http://localhost:8080")]
    [InlineData("https://gotify.local:8443/")]
    public void Validate_WithValidServerUrls_DoesNotThrow(string serverUrl)
    {
        var config = new GotifyConfig
        {
            ServerUrl = serverUrl,
            ApplicationToken = "test-token"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrNullApplicationToken_ThrowsValidationException(string? token)
    {
        var config = new GotifyConfig
        {
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = token ?? string.Empty
        };

        var ex = Should.Throw<ValidationException>(() => config.Validate());
        ex.Message.ShouldContain("required");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(100)]
    public void Validate_WithInvalidPriority_ThrowsValidationException(int priority)
    {
        var config = new GotifyConfig
        {
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "test-token",
            Priority = priority
        };

        var ex = Should.Throw<ValidationException>(() => config.Validate());
        ex.Message.ShouldContain("Priority");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void Validate_WithValidPriority_DoesNotThrow(int priority)
    {
        var config = new GotifyConfig
        {
            ServerUrl = "https://gotify.example.com",
            ApplicationToken = "test-token",
            Priority = priority
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void NewConfig_HasDefaultPriorityOf5()
    {
        var config = new GotifyConfig();

        config.Priority.ShouldBe(5);
    }

    [Fact]
    public void NewConfig_HasEmptyStringsForRequiredFields()
    {
        var config = new GotifyConfig();

        config.ServerUrl.ShouldBe(string.Empty);
        config.ApplicationToken.ShouldBe(string.Empty);
    }

    #endregion
}
