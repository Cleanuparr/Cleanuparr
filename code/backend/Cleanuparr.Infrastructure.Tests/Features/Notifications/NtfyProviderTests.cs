using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Ntfy;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NtfyProviderTests
{
    private readonly Mock<INtfyProxy> _proxyMock;
    private readonly NtfyConfig _config;
    private readonly NtfyProvider _provider;

    public NtfyProviderTests()
    {
        _proxyMock = new Mock<INtfyProxy>();
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
            _proxyMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        // Assert
        Assert.Equal("TestNtfy", _provider.Name);
    }

    [Fact]
    public void Constructor_SetsTypeCorrectly()
    {
        // Assert
        Assert.Equal(NotificationProviderType.Ntfy, _provider.Type);
    }

    #endregion

    #region SendNotificationAsync Tests

    [Fact]
    public async Task SendNotificationAsync_CallsProxyWithCorrectPayload()
    {
        // Arrange
        var context = CreateTestContext();
        NtfyPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NtfyPayload>(), _config))
            .Callback<NtfyPayload, NtfyConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("test-topic", capturedPayload.Topic);
        Assert.Equal(context.Title, capturedPayload.Title);
        Assert.Contains(context.Description, capturedPayload.Message);
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

        var provider = new NtfyProvider("TestNtfy", NotificationProviderType.Ntfy, config, _proxyMock.Object);
        var context = CreateTestContext();

        var capturedPayloads = new List<NtfyPayload>();
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NtfyPayload>(), config))
            .Callback<NtfyPayload, NtfyConfig>((payload, cfg) => capturedPayloads.Add(payload))
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.Equal(3, capturedPayloads.Count);
        Assert.Contains(capturedPayloads, p => p.Topic == "topic1");
        Assert.Contains(capturedPayloads, p => p.Topic == "topic2");
        Assert.Contains(capturedPayloads, p => p.Topic == "topic3");
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesDataInMessage()
    {
        // Arrange
        var context = CreateTestContext();
        context.Data["TestKey"] = "TestValue";
        context.Data["AnotherKey"] = "AnotherValue";

        NtfyPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NtfyPayload>(), _config))
            .Callback<NtfyPayload, NtfyConfig>((payload, config) => capturedPayload = payload)
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
        var config = new NtfyConfig
        {
            Id = Guid.NewGuid(),
            ServerUrl = "http://ntfy.example.com",
            Topics = new List<string> { "test" },
            AuthenticationType = NtfyAuthenticationType.None,
            Priority = NtfyPriority.High,
            Tags = new List<string>()
        };

        var provider = new NtfyProvider("TestNtfy", NotificationProviderType.Ntfy, config, _proxyMock.Object);
        var context = CreateTestContext();

        NtfyPayload? capturedPayload = null;
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NtfyPayload>(), config))
            .Callback<NtfyPayload, NtfyConfig>((payload, cfg) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal((int)NtfyPriority.High, capturedPayload.Priority);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesTagsFromConfig()
    {
        // Arrange
        var context = CreateTestContext();
        NtfyPayload? capturedPayload = null;

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NtfyPayload>(), _config))
            .Callback<NtfyPayload, NtfyConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.NotNull(capturedPayload.Tags);
        Assert.Contains("tag1", capturedPayload.Tags);
        Assert.Contains("tag2", capturedPayload.Tags);
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

        var provider = new NtfyProvider("TestNtfy", NotificationProviderType.Ntfy, config, _proxyMock.Object);
        var context = CreateTestContext();

        NtfyPayload? capturedPayload = null;
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NtfyPayload>(), config))
            .Callback<NtfyPayload, NtfyConfig>((payload, cfg) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("topic-with-spaces", capturedPayload.Topic);
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

        var provider = new NtfyProvider("TestNtfy", NotificationProviderType.Ntfy, config, _proxyMock.Object);
        var context = CreateTestContext();

        var capturedPayloads = new List<NtfyPayload>();
        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NtfyPayload>(), config))
            .Callback<NtfyPayload, NtfyConfig>((payload, cfg) => capturedPayloads.Add(payload))
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        Assert.Equal(2, capturedPayloads.Count);
        Assert.Contains(capturedPayloads, p => p.Topic == "valid-topic");
        Assert.Contains(capturedPayloads, p => p.Topic == "another-valid");
    }

    [Fact]
    public async Task SendNotificationAsync_WhenProxyThrows_PropagatesException()
    {
        // Arrange
        var context = CreateTestContext();

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NtfyPayload>(), _config))
            .ThrowsAsync(new Exception("Proxy error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _provider.SendNotificationAsync(context));
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

        _proxyMock.Setup(p => p.SendNotification(It.IsAny<NtfyPayload>(), _config))
            .Callback<NtfyPayload, NtfyConfig>((payload, config) => capturedPayload = payload)
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
