using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Pushover;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class PushoverProviderTests
{
    private readonly IPushoverProxy _proxy;
    private readonly PushoverConfig _config;
    private readonly PushoverProvider _provider;

    public PushoverProviderTests()
    {
        _proxy = Substitute.For<IPushoverProxy>();
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
            _proxy);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        // Assert
        _provider.Name.ShouldBe("TestPushover");
    }

    [Fact]
    public void Constructor_SetsTypeCorrectly()
    {
        // Assert
        _provider.Type.ShouldBe(NotificationProviderType.Pushover);
    }

    #endregion

    #region SendNotificationAsync Tests

    [Fact]
    public async Task SendNotificationAsync_CallsProxyWithCorrectPayload()
    {
        // Arrange
        var context = CreateTestContext();
        PushoverPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Token.ShouldBe("test-api-token");
        capturedPayload.User.ShouldBe("test-user-key");
        capturedPayload.Title.ShouldBe(context.Title);
        capturedPayload.Message.ShouldContain(context.Description);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesDataInMessage()
    {
        // Arrange
        var context = CreateTestContext();
        context.Data["TestKey"] = "TestValue";
        context.Data["AnotherKey"] = "AnotherValue";

        PushoverPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Message.ShouldContain("TestKey: TestValue");
        capturedPayload.Message.ShouldContain("AnotherKey: AnotherValue");
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

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxy);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Priority.ShouldBe((int)PushoverPriority.High);
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

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxy);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Priority.ShouldBe((int)PushoverPriority.Emergency);
        capturedPayload.Retry.ShouldBe(60);
        capturedPayload.Expire.ShouldBe(3600);
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

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxy);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Retry.ShouldBeNull();
        capturedPayload.Expire.ShouldBeNull();
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

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxy);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Device.ShouldBe("device1,device2,device3");
    }

    [Fact]
    public async Task SendNotificationAsync_WithEmptyDevices_DeviceIsNull()
    {
        // Arrange
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Device.ShouldBeNull();
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

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxy);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Tags.ShouldBe("tag1,tag2");
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

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxy);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Sound.ShouldBe("cosmic");
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
        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        (capturedPayload.Message.Length <= 1024).ShouldBeTrue();
        capturedPayload.Message.ShouldEndWith("...");
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
        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        (capturedPayload.Title!.Length <= 250).ShouldBeTrue();
        capturedPayload.Title.ShouldEndWith("...");
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

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxy);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Device.ShouldBe("device1,device2");
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

        var provider = new PushoverProvider("TestPushover", NotificationProviderType.Pushover, config, _proxy);
        var context = CreateTestContext();

        PushoverPayload? capturedPayload = null;
        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Device.ShouldBe("device1,device2");
    }

    [Fact]
    public async Task SendNotificationAsync_WhenProxyThrows_PropagatesException()
    {
        // Arrange
        var context = CreateTestContext();

        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .ThrowsAsync(new PushoverException("Proxy error"));

        // Act & Assert
        await Should.ThrowAsync<PushoverException>(() => _provider.SendNotificationAsync(context));
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

        _proxy.SendNotification(Arg.Any<PushoverPayload>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<PushoverPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Message.ShouldBe("Test Description Only");
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
