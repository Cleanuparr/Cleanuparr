using Cleanuparr.Persistence.Models.Configuration.Notification;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.Notification;

public sealed class DiscordConfigTests
{
    #region IsValid Tests

    [Fact]
    public void IsValid_WithValidWebhookUrl_ReturnsTrue()
    {
        var config = new DiscordConfig
        {
            WebhookUrl = "https://discord.com/api/webhooks/123456789/abcdefghij"
        };

        config.IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyOrNullWebhookUrl_ReturnsFalse(string? webhookUrl)
    {
        var config = new DiscordConfig
        {
            WebhookUrl = webhookUrl ?? string.Empty
        };

        config.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithOptionalFieldsEmpty_ReturnsTrue()
    {
        var config = new DiscordConfig
        {
            WebhookUrl = "https://discord.com/api/webhooks/123456789/abcdefghij",
            Username = "",
            AvatarUrl = ""
        };

        config.IsValid().ShouldBeTrue();
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WithValidConfig_DoesNotThrow()
    {
        var config = new DiscordConfig
        {
            WebhookUrl = "https://discord.com/api/webhooks/123456789/abcdefghij",
            Username = "Test Bot",
            AvatarUrl = "https://example.com/avatar.png"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrNullWebhookUrl_ThrowsValidationException(string? webhookUrl)
    {
        var config = new DiscordConfig
        {
            WebhookUrl = webhookUrl ?? string.Empty
        };

        var ex = Should.Throw<ValidationException>(() => config.Validate());
        ex.Message.ShouldContain("required");
    }

    [Theory]
    [InlineData("https://example.com/webhook")]
    [InlineData("http://discord.com/api/webhooks/123/abc")]
    [InlineData("not-a-url")]
    [InlineData("https://discord.com/api/something-else")]
    public void Validate_WithInvalidWebhookUrl_ThrowsValidationException(string webhookUrl)
    {
        var config = new DiscordConfig
        {
            WebhookUrl = webhookUrl
        };

        var ex = Should.Throw<ValidationException>(() => config.Validate());
        ex.Message.ShouldContain("valid Discord webhook URL");
    }

    [Theory]
    [InlineData("https://discord.com/api/webhooks/123456789/abcdefghij")]
    [InlineData("https://discordapp.com/api/webhooks/123456789/abcdefghij")]
    public void Validate_WithValidWebhookUrls_DoesNotThrow(string webhookUrl)
    {
        var config = new DiscordConfig
        {
            WebhookUrl = webhookUrl
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithInvalidAvatarUrl_ThrowsValidationException()
    {
        var config = new DiscordConfig
        {
            WebhookUrl = "https://discord.com/api/webhooks/123456789/abcdefghij",
            AvatarUrl = "not-a-valid-url"
        };

        var ex = Should.Throw<ValidationException>(() => config.Validate());
        ex.Message.ShouldContain("valid URL");
    }

    [Fact]
    public void Validate_WithValidAvatarUrl_DoesNotThrow()
    {
        var config = new DiscordConfig
        {
            WebhookUrl = "https://discord.com/api/webhooks/123456789/abcdefghij",
            AvatarUrl = "https://example.com/avatar.png"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithEmptyAvatarUrl_DoesNotThrow()
    {
        var config = new DiscordConfig
        {
            WebhookUrl = "https://discord.com/api/webhooks/123456789/abcdefghij",
            AvatarUrl = ""
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion
}
