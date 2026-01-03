using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Telegram;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Telegram;

public class TelegramProviderTests
{
    private readonly Mock<ITelegramProxy> _proxyMock;
    private readonly TelegramConfig _config;
    private readonly TelegramProvider _provider;

    public TelegramProviderTests()
    {
        _proxyMock = new Mock<ITelegramProxy>();
        _config = new TelegramConfig
        {
            Id = Guid.NewGuid(),
            BotToken = "test-bot-token",
            ChatId = "123456789",
            TopicId = null,
            SendSilently = false
        };

        _provider = new TelegramProvider(
            "TestTelegram",
            NotificationProviderType.Telegram,
            _config,
            _proxyMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        // Assert
        Assert.Equal("TestTelegram", _provider.Name);
    }

    [Fact]
    public void Constructor_SetsTypeCorrectly()
    {
        // Assert
        Assert.Equal(NotificationProviderType.Telegram, _provider.Type);
    }

    #endregion

    #region SendNotificationAsync Tests

    [Fact]
    public async Task SendNotificationAsync_CallsProxyWithCorrectBotToken()
    {
        // Arrange
        var context = CreateTestContext();
        string? capturedBotToken = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((_, token) => capturedBotToken = token)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.Equal("test-bot-token", capturedBotToken);
    }

    [Fact]
    public async Task SendNotificationAsync_CallsProxyWithCorrectChatId()
    {
        // Arrange
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("123456789", capturedPayload.ChatId);
    }

    [Fact]
    public async Task SendNotificationAsync_TrimsChatId()
    {
        // Arrange
        var config = new TelegramConfig
        {
            Id = Guid.NewGuid(),
            BotToken = "token",
            ChatId = "  123456789  ",
            SendSilently = false
        };

        var provider = new TelegramProvider("Test", NotificationProviderType.Telegram, config, _proxyMock.Object);
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("123456789", capturedPayload.ChatId);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesTitleInMessage()
    {
        // Arrange
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Contains("Test Notification", capturedPayload.Text);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesDescriptionInMessage()
    {
        // Arrange
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Contains("Test Description", capturedPayload.Text);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesDataInMessage()
    {
        // Arrange
        var context = CreateTestContext();
        context.Data["TestKey"] = "TestValue";
        context.Data["AnotherKey"] = "AnotherValue";

        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Contains("TestKey: TestValue", capturedPayload.Text);
        Assert.Contains("AnotherKey: AnotherValue", capturedPayload.Text);
    }

    [Fact]
    public async Task SendNotificationAsync_HtmlEncodesSpecialCharacters()
    {
        // Arrange
        var context = new NotificationContext
        {
            EventType = NotificationEventType.Test,
            Title = "Test <script>alert('xss')</script>",
            Description = "Description with & and < and >",
            Severity = EventSeverity.Information,
            Data = new Dictionary<string, string>()
        };

        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Contains("&lt;script&gt;", capturedPayload.Text);
        Assert.Contains("&amp;", capturedPayload.Text);
    }

    [Fact]
    public async Task SendNotificationAsync_WithTopicId_SetsMessageThreadId()
    {
        // Arrange
        var config = new TelegramConfig
        {
            Id = Guid.NewGuid(),
            BotToken = "token",
            ChatId = "123456789",
            TopicId = "42",
            SendSilently = false
        };

        var provider = new TelegramProvider("Test", NotificationProviderType.Telegram, config, _proxyMock.Object);
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal(42, capturedPayload.MessageThreadId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    public async Task SendNotificationAsync_WithInvalidTopicId_SetsMessageThreadIdToNull(string? topicId)
    {
        // Arrange
        var config = new TelegramConfig
        {
            Id = Guid.NewGuid(),
            BotToken = "token",
            ChatId = "123456789",
            TopicId = topicId,
            SendSilently = false
        };

        var provider = new TelegramProvider("Test", NotificationProviderType.Telegram, config, _proxyMock.Object);
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Null(capturedPayload.MessageThreadId);
    }

    [Fact]
    public async Task SendNotificationAsync_WithSendSilently_SetsDisableNotification()
    {
        // Arrange
        var config = new TelegramConfig
        {
            Id = Guid.NewGuid(),
            BotToken = "token",
            ChatId = "123456789",
            SendSilently = true
        };

        var provider = new TelegramProvider("Test", NotificationProviderType.Telegram, config, _proxyMock.Object);
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.True(capturedPayload.DisableNotification);
    }

    [Fact]
    public async Task SendNotificationAsync_WithImage_SetsPhotoUrl()
    {
        // Arrange
        var context = new NotificationContext
        {
            EventType = NotificationEventType.Test,
            Title = "Test Notification",
            Description = "Test Description",
            Severity = EventSeverity.Information,
            Data = new Dictionary<string, string>(),
            Image = new Uri("https://example.com/image.jpg")
        };

        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("https://example.com/image.jpg", capturedPayload.PhotoUrl);
    }

    [Fact]
    public async Task SendNotificationAsync_WithoutImage_PhotoUrlIsNull()
    {
        // Arrange
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Null(capturedPayload.PhotoUrl);
    }

    [Fact]
    public async Task SendNotificationAsync_WithEmptyData_MessageContainsOnlyTitleAndDescription()
    {
        // Arrange
        var context = new NotificationContext
        {
            EventType = NotificationEventType.Test,
            Title = "Test Title",
            Description = "Test Description Only",
            Severity = EventSeverity.Information,
            Data = new Dictionary<string, string>()
        };

        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Contains("Test Title", capturedPayload.Text);
        Assert.Contains("Test Description Only", capturedPayload.Text);
        Assert.DoesNotContain(":", capturedPayload.Text.Replace("Test Title", "").Replace("Test Description Only", "").Trim());
    }

    [Fact]
    public async Task SendNotificationAsync_TrimsWhitespaceFromTitleAndDescription()
    {
        // Arrange
        var context = new NotificationContext
        {
            EventType = NotificationEventType.Test,
            Title = "  Trimmed Title  ",
            Description = "  Trimmed Description  ",
            Severity = EventSeverity.Information,
            Data = new Dictionary<string, string>()
        };

        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.DoesNotContain("  Trimmed", capturedPayload.Text);
        Assert.Contains("Trimmed Title", capturedPayload.Text);
    }

    [Fact]
    public async Task SendNotificationAsync_WhenProxyThrows_PropagatesException()
    {
        // Arrange
        var context = CreateTestContext();

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .ThrowsAsync(new TelegramException("Proxy error"));

        // Act & Assert
        await Assert.ThrowsAsync<TelegramException>(() => _provider.SendNotificationAsync(context));
    }

    [Fact]
    public async Task SendNotificationAsync_WithEmptyTitle_DoesNotIncludeTitleInMessage()
    {
        // Arrange
        var context = new NotificationContext
        {
            EventType = NotificationEventType.Test,
            Title = "   ",
            Description = "Description without title",
            Severity = EventSeverity.Information,
            Data = new Dictionary<string, string>()
        };

        TelegramPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<TelegramPayload>(), It.IsAny<string>()))
            .Callback<TelegramPayload, string>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("Description without title", capturedPayload.Text);
    }

    #endregion

    #region Helper Methods

    private static NotificationContext CreateTestContext()
    {
        return new NotificationContext
        {
            EventType = NotificationEventType.QueueItemDeleted,
            Title = "Test Notification",
            Description = "Test Description",
            Severity = EventSeverity.Information,
            Data = new Dictionary<string, string>()
        };
    }

    #endregion
}
