using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NotifiarrProviderTests
{
    private readonly Mock<INotifiarrProxy> _proxyMock;
    private readonly NotifiarrConfig _config;
    private readonly NotifiarrProvider _provider;

    public NotifiarrProviderTests()
    {
        _proxyMock = new Mock<INotifiarrProxy>();
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
            _proxyMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        // Assert
        Assert.Equal("TestNotifiarr", _provider.Name);
    }

    [Fact]
    public void Constructor_SetsTypeCorrectly()
    {
        // Assert
        Assert.Equal(NotificationProviderType.Notifiarr, _provider.Type);
    }

    #endregion

    #region SendNotificationAsync Tests

    [Fact]
    public async Task SendNotificationAsync_CallsProxyWithCorrectPayload()
    {
        // Arrange
        var context = CreateTestContext();
        NotifiarrPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NotifiarrPayload>(), _config))
            .Callback<NotifiarrPayload, NotifiarrConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.NotNull(capturedPayload.Discord);
        Assert.Equal(context.Title, capturedPayload.Discord.Text.Title);
        Assert.Equal(context.Description, capturedPayload.Discord.Text.Description);
    }

    [Fact]
    public async Task SendNotificationAsync_UsesConfiguredChannelId()
    {
        // Arrange
        var context = CreateTestContext();
        NotifiarrPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NotifiarrPayload>(), _config))
            .Callback<NotifiarrPayload, NotifiarrConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("123456789012345678", capturedPayload.Discord.Ids.Channel);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesDataAsFields()
    {
        // Arrange
        var context = CreateTestContext();
        context.Data["TestKey"] = "TestValue";
        context.Data["AnotherKey"] = "AnotherValue";

        NotifiarrPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NotifiarrPayload>(), _config))
            .Callback<NotifiarrPayload, NotifiarrConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal(2, capturedPayload.Discord.Text.Fields.Count);
        Assert.Contains(capturedPayload.Discord.Text.Fields, f => f.Title == "TestKey" && f.Text == "TestValue");
        Assert.Contains(capturedPayload.Discord.Text.Fields, f => f.Title == "AnotherKey" && f.Text == "AnotherValue");
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

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NotifiarrPayload>(), _config))
            .Callback<NotifiarrPayload, NotifiarrConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal(expectedColor, capturedPayload.Discord.Color);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesCleanuperrLogo()
    {
        // Arrange
        var context = CreateTestContext();
        NotifiarrPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NotifiarrPayload>(), _config))
            .Callback<NotifiarrPayload, NotifiarrConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Contains("Cleanuparr", capturedPayload.Discord.Text.Icon);
        Assert.NotNull(capturedPayload.Discord.Images.Thumbnail);
        Assert.Contains("Cleanuparr", capturedPayload.Discord.Images.Thumbnail.ToString());
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesContextImage()
    {
        // Arrange
        var context = CreateTestContext();
        context.Image = new Uri("https://example.com/image.jpg");

        NotifiarrPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NotifiarrPayload>(), _config))
            .Callback<NotifiarrPayload, NotifiarrConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal(new Uri("https://example.com/image.jpg"), capturedPayload.Discord.Images.Image);
    }

    [Fact]
    public async Task SendNotificationAsync_WhenNoImage_ImagesImageIsNull()
    {
        // Arrange
        var context = CreateTestContext();
        context.Image = null;

        NotifiarrPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NotifiarrPayload>(), _config))
            .Callback<NotifiarrPayload, NotifiarrConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Null(capturedPayload.Discord.Images.Image);
    }

    [Fact]
    public async Task SendNotificationAsync_WhenProxyThrows_PropagatesException()
    {
        // Arrange
        var context = CreateTestContext();

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NotifiarrPayload>(), _config))
            .ThrowsAsync(new Exception("Proxy error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _provider.SendNotificationAsync(context));
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

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NotifiarrPayload>(), _config))
            .Callback<NotifiarrPayload, NotifiarrConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Empty(capturedPayload.Discord.Text.Fields);
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
