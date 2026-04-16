using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NotifiarrProviderTests
{
    private readonly INotifiarrProxy _proxy;
    private readonly NotifiarrConfig _config;
    private readonly NotifiarrProvider _provider;

    public NotifiarrProviderTests()
    {
        _proxy = Substitute.For<INotifiarrProxy>();
        _config = new NotifiarrConfig
        {
            Id = Guid.NewGuid(),
            ApiKey = "testapikey1234567890",
            ChannelId = "123456789012345678"
        };

        _provider = new NotifiarrProvider(
            "TestNotifiarr",
            NotificationProviderType.Notifiarr,
            _config,
            _proxy);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        // Assert
        _provider.Name.ShouldBe("TestNotifiarr");
    }

    [Fact]
    public void Constructor_SetsTypeCorrectly()
    {
        // Assert
        _provider.Type.ShouldBe(NotificationProviderType.Notifiarr);
    }

    #endregion

    #region SendNotificationAsync Tests

    [Fact]
    public async Task SendNotificationAsync_CallsProxyWithCorrectPayload()
    {
        // Arrange
        var context = CreateTestContext();
        NotifiarrPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<NotifiarrPayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NotifiarrPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Discord.ShouldNotBeNull();
        capturedPayload.Discord.Text.Title.ShouldBe(context.Title);
        capturedPayload.Discord.Text.Description.ShouldBe(context.Description);
    }

    [Fact]
    public async Task SendNotificationAsync_UsesConfiguredChannelId()
    {
        // Arrange
        var context = CreateTestContext();
        NotifiarrPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<NotifiarrPayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NotifiarrPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Discord.Ids.Channel.ShouldBe("123456789012345678");
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesDataAsFields()
    {
        // Arrange
        var context = CreateTestContext();
        context.Data["TestKey"] = "TestValue";
        context.Data["AnotherKey"] = "AnotherValue";

        NotifiarrPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<NotifiarrPayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NotifiarrPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Discord.Text.Fields.Count.ShouldBe(2);
        capturedPayload.Discord.Text.Fields.ShouldContain(f => f.Title == "TestKey" && f.Text == "TestValue");
        capturedPayload.Discord.Text.Fields.ShouldContain(f => f.Title == "AnotherKey" && f.Text == "AnotherValue");
    }

    [Theory]
    [InlineData(EventSeverity.Information, "28a745")]  // Green
    [InlineData(EventSeverity.Warning, "f0ad4e")]      // Orange
    [InlineData(EventSeverity.Important, "bb2124")]    // Red
    public async Task SendNotificationAsync_MapsEventSeverityToCorrectColor(EventSeverity severity, string expectedColor)
    {
        // Arrange
        var context = new NotificationContext
        {
            EventType = NotificationEventType.QueueItemDeleted,
            Title = "Test Notification",
            Description = "Test Description",
            Severity = severity,
            Data = new Dictionary<string, string>()
        };

        NotifiarrPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<NotifiarrPayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NotifiarrPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Discord.Color.ShouldBe(expectedColor);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesCleanuperrLogo()
    {
        // Arrange
        var context = CreateTestContext();
        NotifiarrPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<NotifiarrPayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NotifiarrPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Discord.Text.Icon.ShouldContain("Cleanuparr");
        capturedPayload.Discord.Images.Thumbnail.ShouldNotBeNull();
        capturedPayload.Discord.Images.Thumbnail.ToString().ShouldContain("Cleanuparr");
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesContextImage()
    {
        // Arrange
        var context = CreateTestContext();
        context.Image = new Uri("https://example.com/image.jpg");

        NotifiarrPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<NotifiarrPayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NotifiarrPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Discord.Images.Image.ShouldBe(new Uri("https://example.com/image.jpg"));
    }

    [Fact]
    public async Task SendNotificationAsync_WhenNoImage_ImagesImageIsNull()
    {
        // Arrange
        var context = CreateTestContext();
        context.Image = null;

        NotifiarrPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<NotifiarrPayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NotifiarrPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Discord.Images.Image.ShouldBeNull();
    }

    [Fact]
    public async Task SendNotificationAsync_WhenProxyThrows_PropagatesException()
    {
        // Arrange
        var context = CreateTestContext();

        _proxy.SendNotification(Arg.Any<NotifiarrPayload>(), _config)
            .ThrowsAsync(new Exception("Proxy error"));

        // Act & Assert
        await Should.ThrowAsync<Exception>(() => _provider.SendNotificationAsync(context));
    }

    [Fact]
    public async Task SendNotificationAsync_WithEmptyData_HasEmptyFields()
    {
        // Arrange
        var context = new NotificationContext
        {
            EventType = NotificationEventType.Test,
            Title = "Test Title",
            Description = "Test Description",
            Severity = EventSeverity.Information,
            Data = new Dictionary<string, string>()
        };

        NotifiarrPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<NotifiarrPayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NotifiarrPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Discord.Text.Fields.ShouldBeEmpty();
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
