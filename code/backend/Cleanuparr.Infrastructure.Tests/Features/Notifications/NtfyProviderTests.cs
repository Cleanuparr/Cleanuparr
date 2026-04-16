using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Ntfy;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NtfyProviderTests
{
    private readonly INtfyProxy _proxy;
    private readonly NtfyConfig _config;
    private readonly NtfyProvider _provider;

    public NtfyProviderTests()
    {
        _proxy = Substitute.For<INtfyProxy>();
        _config = new NtfyConfig
        {
            Id = Guid.NewGuid(),
            ServerUrl = "http://ntfy.example.com",
            Topics = new List<string> { "test-topic" },
            AuthenticationType = NtfyAuthenticationType.None,
            Priority = NtfyPriority.Default,
            Tags = new List<string> { "tag1", "tag2" }
        };

        _provider = new NtfyProvider(
            "TestNtfy",
            NotificationProviderType.Ntfy,
            _config,
            _proxy);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        // Assert
        _provider.Name.ShouldBe("TestNtfy");
    }

    [Fact]
    public void Constructor_SetsTypeCorrectly()
    {
        // Assert
        _provider.Type.ShouldBe(NotificationProviderType.Ntfy);
    }

    #endregion

    #region SendNotificationAsync Tests

    [Fact]
    public async Task SendNotificationAsync_CallsProxyWithCorrectPayload()
    {
        // Arrange
        var context = CreateTestContext();
        NtfyPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<NtfyPayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NtfyPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Topic.ShouldBe("test-topic");
        capturedPayload.Title.ShouldBe(context.Title);
        capturedPayload.Message.ShouldContain(context.Description);
    }

    [Fact]
    public async Task SendNotificationAsync_WithMultipleTopics_SendsToAllTopics()
    {
        // Arrange
        var config = new NtfyConfig
        {
            Id = Guid.NewGuid(),
            ServerUrl = "http://ntfy.example.com",
            Topics = new List<string> { "topic1", "topic2", "topic3" },
            AuthenticationType = NtfyAuthenticationType.None,
            Priority = NtfyPriority.Default,
            Tags = new List<string>()
        };

        var provider = new NtfyProvider("TestNtfy", NotificationProviderType.Ntfy, config, _proxy);
        var context = CreateTestContext();

        var capturedPayloads = new List<NtfyPayload>();
        _proxy.SendNotification(Arg.Any<NtfyPayload>(), config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayloads.Add(ci.ArgAt<NtfyPayload>(0)));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayloads.Count.ShouldBe(3);
        capturedPayloads.ShouldContain(p => p.Topic == "topic1");
        capturedPayloads.ShouldContain(p => p.Topic == "topic2");
        capturedPayloads.ShouldContain(p => p.Topic == "topic3");
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesDataInMessage()
    {
        // Arrange
        var context = CreateTestContext();
        context.Data["TestKey"] = "TestValue";
        context.Data["AnotherKey"] = "AnotherValue";

        NtfyPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<NtfyPayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NtfyPayload>(0));

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
        var config = new NtfyConfig
        {
            Id = Guid.NewGuid(),
            ServerUrl = "http://ntfy.example.com",
            Topics = new List<string> { "test" },
            AuthenticationType = NtfyAuthenticationType.None,
            Priority = NtfyPriority.High,
            Tags = new List<string>()
        };

        var provider = new NtfyProvider("TestNtfy", NotificationProviderType.Ntfy, config, _proxy);
        var context = CreateTestContext();

        NtfyPayload? capturedPayload = null;
        _proxy.SendNotification(Arg.Any<NtfyPayload>(), config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NtfyPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Priority.ShouldBe((int)NtfyPriority.High);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesTagsFromConfig()
    {
        // Arrange
        var context = CreateTestContext();
        NtfyPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<NtfyPayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NtfyPayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Tags.ShouldNotBeNull();
        capturedPayload.Tags.ShouldContain("tag1");
        capturedPayload.Tags.ShouldContain("tag2");
    }

    [Fact]
    public async Task SendNotificationAsync_TrimsTopicNames()
    {
        // Arrange
        var config = new NtfyConfig
        {
            Id = Guid.NewGuid(),
            ServerUrl = "http://ntfy.example.com",
            Topics = new List<string> { "  topic-with-spaces  " },
            AuthenticationType = NtfyAuthenticationType.None,
            Priority = NtfyPriority.Default,
            Tags = new List<string>()
        };

        var provider = new NtfyProvider("TestNtfy", NotificationProviderType.Ntfy, config, _proxy);
        var context = CreateTestContext();

        NtfyPayload? capturedPayload = null;
        _proxy.SendNotification(Arg.Any<NtfyPayload>(), config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NtfyPayload>(0));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Topic.ShouldBe("topic-with-spaces");
    }

    [Fact]
    public async Task SendNotificationAsync_SkipsEmptyTopics()
    {
        // Arrange
        var config = new NtfyConfig
        {
            Id = Guid.NewGuid(),
            ServerUrl = "http://ntfy.example.com",
            Topics = new List<string> { "valid-topic", "", "  ", "another-valid" },
            AuthenticationType = NtfyAuthenticationType.None,
            Priority = NtfyPriority.Default,
            Tags = new List<string>()
        };

        var provider = new NtfyProvider("TestNtfy", NotificationProviderType.Ntfy, config, _proxy);
        var context = CreateTestContext();

        var capturedPayloads = new List<NtfyPayload>();
        _proxy.SendNotification(Arg.Any<NtfyPayload>(), config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayloads.Add(ci.ArgAt<NtfyPayload>(0)));

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        capturedPayloads.Count.ShouldBe(2);
        capturedPayloads.ShouldContain(p => p.Topic == "valid-topic");
        capturedPayloads.ShouldContain(p => p.Topic == "another-valid");
    }

    [Fact]
    public async Task SendNotificationAsync_WhenProxyThrows_PropagatesException()
    {
        // Arrange
        var context = CreateTestContext();

        _proxy.SendNotification(Arg.Any<NtfyPayload>(), _config)
            .ThrowsAsync(new Exception("Proxy error"));

        // Act & Assert
        await Should.ThrowAsync<Exception>(() => _provider.SendNotificationAsync(context));
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

        NtfyPayload? capturedPayload = null;

        _proxy.SendNotification(Arg.Any<NtfyPayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<NtfyPayload>(0));

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
