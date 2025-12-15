using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Apprise;

public class AppriseCliProxyTests
{
    private readonly AppriseCliProxy _proxy;

    public AppriseCliProxyTests()
    {
        _proxy = new AppriseCliProxy();
    }

    private static ApprisePayload CreatePayload(string title = "Test Title", string body = "Test Body")
    {
        return new ApprisePayload
        {
            Title = title,
            Body = body,
            Type = "info"
        };
    }

    private static AppriseConfig CreateConfig(string? serviceUrls = null)
    {
        return new AppriseConfig
        {
            ServiceUrls = serviceUrls
        };
    }

    #region SendNotification Validation Tests

    [Fact]
    public async Task SendNotification_WhenServiceUrlsIsNull_ThrowsAppriseException()
    {
        // Arrange
        var config = CreateConfig(null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppriseException>(() =>
            _proxy.SendNotification(CreatePayload(), config));
        Assert.Contains("No service URLs configured", ex.Message);
    }

    [Fact]
    public async Task SendNotification_WhenServiceUrlsIsEmpty_ThrowsAppriseException()
    {
        // Arrange
        var config = CreateConfig("");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppriseException>(() =>
            _proxy.SendNotification(CreatePayload(), config));
        Assert.Contains("No service URLs configured", ex.Message);
    }

    [Fact]
    public async Task SendNotification_WhenServiceUrlsIsWhitespace_ThrowsAppriseException()
    {
        // Arrange
        var config = CreateConfig("   \n   \n   ");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppriseException>(() =>
            _proxy.SendNotification(CreatePayload(), config));
        Assert.Contains("No service URLs configured", ex.Message);
    }

    #endregion

    #region BuildCommand Tests

    [Fact]
    public void BuildCommand_WithSingleUrl_ReturnsCorrectCommand()
    {
        // Arrange
        var payload = CreatePayload("Test", "Hello World");
        var urls = new[] { "discord://webhook_id/webhook_token" };

        // Act
        string arguments = AppriseCliProxy.BuildArguments(payload, urls);

        // Assert
        Assert.Contains("--title=\"Test\"", arguments);
        Assert.Contains("--body=\"Hello World\"", arguments);
        Assert.Contains("--notification-type=info", arguments);
        Assert.Contains("\"discord://webhook_id/webhook_token\"", arguments);
    }

    [Fact]
    public void BuildCommand_WithMultipleUrls_IncludesAllUrls()
    {
        // Arrange
        var payload = CreatePayload("Test", "Body");
        var urls = new[]
        {
            "discord://webhook_id/token",
            "slack://token_a/token_b/token_c",
            "telegram://bot_token/chat_id"
        };

        // Act
        var command = AppriseCliProxy.BuildArguments(payload, urls);

        // Assert
        Assert.Contains("\"discord://webhook_id/token\"", command);
        Assert.Contains("\"slack://token_a/token_b/token_c\"", command);
        Assert.Contains("\"telegram://bot_token/chat_id\"", command);
    }

    [Fact]
    public void BuildCommand_WithNullTitle_OmitsTitle()
    {
        // Arrange
        var payload = new ApprisePayload { Title = null, Body = "Body", Type = "info" };
        var urls = new[] { "discord://id/token" };

        // Act
        var command = AppriseCliProxy.BuildArguments(payload, urls);

        // Assert
        Assert.DoesNotContain("--title=", command);
        Assert.Contains("--body=\"Body\"", command);
    }

    [Fact]
    public void BuildCommand_WithEmptyTitle_OmitsTitle()
    {
        // Arrange
        var payload = new ApprisePayload { Title = "", Body = "Body", Type = "info" };
        var urls = new[] { "discord://id/token" };

        // Act
        var command = AppriseCliProxy.BuildArguments(payload, urls);

        // Assert
        Assert.DoesNotContain("--title=", command);
    }

    [Theory]
    [InlineData("info")]
    [InlineData("success")]
    [InlineData("warning")]
    [InlineData("failure")]
    public void BuildCommand_WithDifferentTypes_SetsCorrectType(string notificationType)
    {
        // Arrange
        var payload = new ApprisePayload { Title = "T", Body = "B", Type = notificationType };
        var urls = new[] { "discord://id/token" };

        // Act
        var command = AppriseCliProxy.BuildArguments(payload, urls);

        // Assert
        Assert.Contains($"--notification-type={notificationType}", command);
    }

    #endregion

    #region EscapeArgument Tests

    [Fact]
    public void EscapeArgument_WithNormalText_ReturnsUnchanged()
    {
        // Act
        var result = AppriseCliProxy.EscapeArgument("Hello World");

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void EscapeArgument_WithDoubleQuotes_EscapesThem()
    {
        // Act
        var result = AppriseCliProxy.EscapeArgument("Say \"Hello\"");

        // Assert
        Assert.Equal("Say \\\"Hello\\\"", result);
    }

    [Fact]
    public void EscapeArgument_WithBackslashes_EscapesThem()
    {
        // Act
        var result = AppriseCliProxy.EscapeArgument("C:\\path\\file");

        // Assert
        Assert.Equal("C:\\\\path\\\\file", result);
    }

    [Fact]
    public void EscapeArgument_WithMixedSpecialChars_EscapesBoth()
    {
        // Act
        var result = AppriseCliProxy.EscapeArgument("Say \"Hello\\World\"");

        // Assert
        Assert.Equal("Say \\\"Hello\\\\World\\\"", result);
    }

    [Fact]
    public void EscapeArgument_WithEmptyString_ReturnsEmpty()
    {
        // Act
        var result = AppriseCliProxy.EscapeArgument("");

        // Assert
        Assert.Equal("", result);
    }

    #endregion
}
