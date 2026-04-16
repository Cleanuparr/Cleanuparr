using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Telegram;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications.Telegram;

public class TelegramProviderTests
{
    private readonly ITelegramProxy _proxy;
    private readonly TelegramConfig _config;
    private readonly TelegramProvider _provider;

    public TelegramProviderTests()
    {
        _proxy = Substitute.For<ITelegramProxy>();
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
            _proxy);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        // Assert
        _provider.Name.ShouldBe("TestTelegram");
    }

    [Fact]
    public void Constructor_SetsTypeCorrectly()
    {
        // Assert
        _provider.Type.ShouldBe(NotificationProviderType.Telegram);
    }

    #endregion

    #region SendNotificationAsync Tests

    [Fact]
    public async Task SendNotificationAsync_CallsProxyWithCorrectBotToken()
    {
        // Arrange
        var context = CreateTestContext();
        string? capturedBotToken = null;

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedBotToken = ci.ArgAt<string>(1));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedBotToken.ShouldBe("test-bot-token");
    }

    [Fact]
    public async Task SendNotificationAsync_CallsProxyWithCorrectChatId()
    {
        // Arrange
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.ChatId.ShouldBe("123456789");
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

        var provider = new TelegramProvider("Test", NotificationProviderType.Telegram, config, _proxy);
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.ChatId.ShouldBe("123456789");
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesTitleInMessage()
    {
        // Arrange
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Text.ShouldContain("Test Notification");
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesDescriptionInMessage()
    {
        // Arrange
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Text.ShouldContain("Test Description");
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesDataInMessage()
    {
        // Arrange
        var context = CreateTestContext();
        context.Data["TestKey"] = "TestValue";
        context.Data["AnotherKey"] = "AnotherValue";

        TelegramPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Text.ShouldContain("TestKey: TestValue");
        capturedPayload.Text.ShouldContain("AnotherKey: AnotherValue");
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

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Text.ShouldContain("&lt;script&gt;");
        capturedPayload.Text.ShouldContain("&amp;");
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

        var provider = new TelegramProvider("Test", NotificationProviderType.Telegram, config, _proxy);
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.MessageThreadId.ShouldBe(42);
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

        var provider = new TelegramProvider("Test", NotificationProviderType.Telegram, config, _proxy);
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.MessageThreadId.ShouldBeNull();
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

        var provider = new TelegramProvider("Test", NotificationProviderType.Telegram, config, _proxy);
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.DisableNotification.ShouldBeTrue();
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

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.PhotoUrl.ShouldBe("https://example.com/image.jpg");
    }

    [Fact]
    public async Task SendNotificationAsync_WithoutImage_PhotoUrlIsNull()
    {
        // Arrange
        var context = CreateTestContext();
        TelegramPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.PhotoUrl.ShouldBeNull();
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

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Text.ShouldContain("Test Title");
        capturedPayload.Text.ShouldContain("Test Description Only");
        capturedPayload.Text.Replace("Test Title", "").Replace("Test Description Only", "").Trim().ShouldNotContain(":");
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

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Text.ShouldNotContain("  Trimmed");
        capturedPayload.Text.ShouldContain("Trimmed Title");
    }

    [Fact]
    public async Task SendNotificationAsync_WhenProxyThrows_PropagatesException()
    {
        // Arrange
        var context = CreateTestContext();

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .ThrowsAsync(new TelegramException("Proxy error"));

        // Act & Assert
        await Should.ThrowAsync<TelegramException>(() => _provider.SendNotificationAsync(context));
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

        _proxy.SendNotification(Arg.Any<TelegramPayload>(), Arg.Any<string>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<TelegramPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Text.ShouldBe("Description without title");
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
