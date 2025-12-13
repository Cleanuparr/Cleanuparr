using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Pushover;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class PushoverProviderTests
{
    private readonly Mock<IPushoverProxy> _proxyMock;
    private readonly PushoverConfig _config;
    private readonly PushoverProvider _provider;

    public PushoverProviderTests()
    {
        _proxyMock = new Mock<IPushoverProxy>();
        _config = new PushoverConfig
        {
            Id = Guid.NewGuid(),
            ApiToken = "test-api-token",
            UserKey = "test-user-key",
            Devices = new List<string>(),
            Priority = PushoverPriority.Normal,
            Sound = "",
            Retry = null,
            Expire = null,
            Tags = new List<string>()
        };

        _provider = new PushoverProvider(
            "TestPushover",
            NotificationProviderType.Pushover,
            _config,
            _proxyMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        // Assert
        Assert.Equal("TestPushover", _provider.Name);
    }

    [Fact]
    public void Constructor_SetsTypeCorrectly()
    {
        // Assert
        Assert.Equal(NotificationProviderType.Pushover, _provider.Type);
    }

    #endregion

    #region SendNotificationAsync Tests

    [Fact]
    public async Task SendNotificationAsync_CallsProxyWithCorrectPayload()
    {
        // Arrange
        var context = CreateTestContext();
        PushoverPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("test-api-token", capturedPayload.Token);
        Assert.Equal("test-user-key", capturedPayload.User);
        Assert.Equal(context.Title, capturedPayload.Title);
        Assert.Contains(context.Description, capturedPayload.Message);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesDataInMessage()
    {
        // Arrange
        var context = CreateTestContext();
        context.Data["TestKey"] = "TestValue";
        context.Data["AnotherKey"] = "AnotherValue";

        PushoverPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Contains("TestKey: TestValue", capturedPayload.Message);
        Assert.Contains("AnotherKey: AnotherValue", capturedPayload.Message);
    }

    [Fact]
    public async Task SendNotificationAsync_UsesPriorityFromConfig()
    {
        // Arrange
        var config = new PushoverConfig
        {
            Id = Guid.NewGuid(),
            ApiToken = "token",
            UserKey = "user",
            Devices = new List<string>(),
            Priority = PushoverPriority.High,
            Sound = "",
            Tags = new List<string>()
        };

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxyMock.Object);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal((int)PushoverPriority.High, capturedPayload.Priority);
    }

    [Fact]
    public async Task SendNotificationAsync_WithEmergencyPriority_IncludesRetryAndExpire()
    {
        // Arrange
        var config = new PushoverConfig
        {
            Id = Guid.NewGuid(),
            ApiToken = "token",
            UserKey = "user",
            Devices = new List<string>(),
            Priority = PushoverPriority.Emergency,
            Sound = "",
            Retry = 60,
            Expire = 3600,
            Tags = new List<string>()
        };

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxyMock.Object);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal((int)PushoverPriority.Emergency, capturedPayload.Priority);
        Assert.Equal(60, capturedPayload.Retry);
        Assert.Equal(3600, capturedPayload.Expire);
    }

    [Fact]
    public async Task SendNotificationAsync_WithNonEmergencyPriority_DoesNotIncludeRetryAndExpire()
    {
        // Arrange
        var config = new PushoverConfig
        {
            Id = Guid.NewGuid(),
            ApiToken = "token",
            UserKey = "user",
            Devices = new List<string>(),
            Priority = PushoverPriority.High, // Not Emergency
            Sound = "",
            Retry = 60, // Should be ignored
            Expire = 3600, // Should be ignored
            Tags = new List<string>()
        };

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxyMock.Object);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Null(capturedPayload.Retry);
        Assert.Null(capturedPayload.Expire);
    }

    [Fact]
    public async Task SendNotificationAsync_WithDevices_JoinsDevicesAsString()
    {
        // Arrange
        var config = new PushoverConfig
        {
            Id = Guid.NewGuid(),
            ApiToken = "token",
            UserKey = "user",
            Devices = new List<string> { "device1", "device2", "device3" },
            Priority = PushoverPriority.Normal,
            Sound = "",
            Tags = new List<string>()
        };

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxyMock.Object);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("device1,device2,device3", capturedPayload.Device);
    }

    [Fact]
    public async Task SendNotificationAsync_WithEmptyDevices_DeviceIsNull()
    {
        // Arrange
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Null(capturedPayload.Device);
    }

    [Fact]
    public async Task SendNotificationAsync_WithTags_JoinsTagsAsString()
    {
        // Arrange
        var config = new PushoverConfig
        {
            Id = Guid.NewGuid(),
            ApiToken = "token",
            UserKey = "user",
            Devices = new List<string>(),
            Priority = PushoverPriority.Normal,
            Sound = "",
            Tags = new List<string> { "tag1", "tag2" }
        };

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxyMock.Object);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("tag1,tag2", capturedPayload.Tags);
    }

    [Fact]
    public async Task SendNotificationAsync_WithSound_IncludesSound()
    {
        // Arrange
        var config = new PushoverConfig
        {
            Id = Guid.NewGuid(),
            ApiToken = "token",
            UserKey = "user",
            Devices = new List<string>(),
            Priority = PushoverPriority.Normal,
            Sound = "cosmic",
            Tags = new List<string>()
        };

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxyMock.Object);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("cosmic", capturedPayload.Sound);
    }

    [Fact]
    public async Task SendNotificationAsync_TruncatesLongMessage()
    {
        // Arrange
        var context = new NotificationContext
        {
            EventType = NotificationEventType.QueueItemDeleted,
            Title = "Test Notification",
            Description = new string('A', 2000), // Very long message
            Severity = EventSeverity.Information,
            Data = new Dictionary<string, string>()
        };

        PushoverPayload? capturedPayload = null;
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.True(capturedPayload.Message.Length <= 1024);
        Assert.EndsWith("...", capturedPayload.Message);
    }

    [Fact]
    public async Task SendNotificationAsync_TruncatesLongTitle()
    {
        // Arrange
        var context = new NotificationContext
        {
            EventType = NotificationEventType.QueueItemDeleted,
            Title = new string('B', 300), // Very long title
            Description = "Test Description",
            Severity = EventSeverity.Information,
            Data = new Dictionary<string, string>()
        };

        PushoverPayload? capturedPayload = null;
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.True(capturedPayload.Title!.Length <= 250);
        Assert.EndsWith("...", capturedPayload.Title);
    }

    [Fact]
    public async Task SendNotificationAsync_TrimsDeviceNames()
    {
        // Arrange
        var config = new PushoverConfig
        {
            Id = Guid.NewGuid(),
            ApiToken = "token",
            UserKey = "user",
            Devices = new List<string> { "  device1  ", "device2  " },
            Priority = PushoverPriority.Normal,
            Sound = "",
            Tags = new List<string>()
        };

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxyMock.Object);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("device1,device2", capturedPayload.Device);
    }

    [Fact]
    public async Task SendNotificationAsync_SkipsEmptyDevices()
    {
        // Arrange
        var config = new PushoverConfig
        {
            Id = Guid.NewGuid(),
            ApiToken = "token",
            UserKey = "user",
            Devices = new List<string> { "device1", "", "  ", "device2" },
            Priority = PushoverPriority.Normal,
            Sound = "",
            Tags = new List<string>()
        };

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxyMock.Object);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("device1,device2", capturedPayload.Device);
    }

    [Fact]
    public async Task SendNotificationAsync_WhenProxyThrows_PropagatesException()
    {
        // Arrange
        var context = CreateTestContext();

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .ThrowsAsync(new PushoverException("Proxy error"));

        // Act & Assert
        await Assert.ThrowsAsync<PushoverException>(() => _provider.SendNotificationAsync(context));
    }

    [Fact]
    public async Task SendNotificationAsync_WithEmptyData_MessageContainsOnlyDescription()
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

        PushoverPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<PushoverPayload>()))
            .Callback<PushoverPayload>(payload => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("Test Description Only", capturedPayload.Message);
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
