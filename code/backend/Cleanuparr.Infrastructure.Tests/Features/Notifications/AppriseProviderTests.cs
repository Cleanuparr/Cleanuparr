using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class AppriseProviderTests
{
    private readonly Mock<IAppriseProxy> _apiProxyMock;
    private readonly Mock<IAppriseCliProxy> _cliProxyMock;
    private readonly AppriseConfig _config;
    private readonly AppriseProvider _provider;

    public AppriseProviderTests()
    {
        _apiProxyMock = new Mock<IAppriseProxy>();
        _cliProxyMock = new Mock<IAppriseCliProxy>();
        _config = new AppriseConfig
        {
            Id = Guid.NewGuid(),
            Mode = AppriseMode.Api,
            Url = "http://apprise.example.com",
            Key = "testkey",
            Tags = "tag1,tag2"
        };

        _provider = new AppriseProvider(
            "TestApprise",
            NotificationProviderType.Apprise,
            _config,
            _apiProxyMock.Object,
            _cliProxyMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        // Assert
        Assert.Equal("TestApprise", _provider.Name);
    }

    [Fact]
    public void Constructor_SetsTypeCorrectly()
    {
        // Assert
        Assert.Equal(NotificationProviderType.Apprise, _provider.Type);
    }

    #endregion

    #region SendNotificationAsync Tests

    [Fact]
    public async Task SendNotificationAsync_CallsProxyWithCorrectPayload()
    {
        // Arrange
        var context = CreateTestContext();
        ApprisePayload? capturedPayload = null;

        _apiProxyMock.Setup(p => p.SendNotification(It.IsAny<ApprisePayload>(), _config))
            .Callback<ApprisePayload, AppriseConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal(context.Title, capturedPayload.Title);
        Assert.Contains(context.Description, capturedPayload.Body);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesDataInBody()
    {
        // Arrange
        var context = CreateTestContext();
        context.Data["TestKey"] = "TestValue";
        context.Data["AnotherKey"] = "AnotherValue";

        ApprisePayload? capturedPayload = null;

        _apiProxyMock.Setup(p => p.SendNotification(It.IsAny<ApprisePayload>(), _config))
            .Callback<ApprisePayload, AppriseConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Contains("TestKey: TestValue", capturedPayload.Body);
        Assert.Contains("AnotherKey: AnotherValue", capturedPayload.Body);
    }

    [Theory]
    [InlineData(EventSeverity.Information, "info")]
    [InlineData(EventSeverity.Warning, "warning")]
    [InlineData(EventSeverity.Important, "failure")]
    public async Task SendNotificationAsync_MapsEventSeverityToCorrectType(EventSeverity severity, string expectedType)
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

        ApprisePayload? capturedPayload = null;

        _apiProxyMock.Setup(p => p.SendNotification(It.IsAny<ApprisePayload>(), _config))
            .Callback<ApprisePayload, AppriseConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal(expectedType, capturedPayload.Type);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesTagsFromConfig()
    {
        // Arrange
        var context = CreateTestContext();
        ApprisePayload? capturedPayload = null;

        _apiProxyMock.Setup(p => p.SendNotification(It.IsAny<ApprisePayload>(), _config))
            .Callback<ApprisePayload, AppriseConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Equal("tag1,tag2", capturedPayload.Tags);
    }

    [Fact]
    public async Task SendNotificationAsync_WhenProxyThrows_PropagatesException()
    {
        // Arrange
        var context = CreateTestContext();

        _apiProxyMock.Setup(p => p.SendNotification(It.IsAny<ApprisePayload>(), _config))
            .ThrowsAsync(new Exception("Proxy error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _provider.SendNotificationAsync(context));
    }

    [Fact]
    public async Task SendNotificationAsync_WithEmptyData_StillIncludesDescription()
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

        ApprisePayload? capturedPayload = null;

        _apiProxyMock.Setup(p => p.SendNotification(It.IsAny<ApprisePayload>(), _config))
            .Callback<ApprisePayload, AppriseConfig>((payload, config) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        Assert.NotNull(capturedPayload);
        Assert.Contains("Test Description", capturedPayload.Body);
    }

    [Fact]
    public async Task SendNotificationAsync_CliMode_CallsCliProxy()
    {
        // Arrange
        var cliConfig = new AppriseConfig
        {
            Id = Guid.NewGuid(),
            Mode = AppriseMode.Cli,
            ServiceUrls = "discord://webhook_id/token"
        };

        var apiProxyMock = new Mock<IAppriseProxy>();
        var cliProxyMock = new Mock<IAppriseCliProxy>();

        var provider = new AppriseProvider(
            "TestAppriseCli",
            NotificationProviderType.Apprise,
            cliConfig,
            apiProxyMock.Object,
            cliProxyMock.Object);

        var context = CreateTestContext();

        cliProxyMock.Setup(p => p.SendNotification(It.IsAny<ApprisePayload>(), cliConfig))
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        cliProxyMock.Verify(p => p.SendNotification(It.IsAny<ApprisePayload>(), cliConfig), Times.Once);
        apiProxyMock.Verify(p => p.SendNotification(It.IsAny<ApprisePayload>(), It.IsAny<AppriseConfig>()), Times.Never);
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
