using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class AppriseProviderTests
{
    private readonly IAppriseProxy _apiProxy;
    private readonly IAppriseCliProxy _cliProxy;
    private readonly AppriseConfig _config;
    private readonly AppriseProvider _provider;

    public AppriseProviderTests()
    {
        _apiProxy = Substitute.For<IAppriseProxy>();
        _cliProxy = Substitute.For<IAppriseCliProxy>();
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
            _apiProxy,
            _cliProxy);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        // Assert
        _provider.Name.ShouldBe("TestApprise");
    }

    [Fact]
    public void Constructor_SetsTypeCorrectly()
    {
        // Assert
        _provider.Type.ShouldBe(NotificationProviderType.Apprise);
    }

    #endregion

    #region SendNotificationAsync Tests

    [Fact]
    public async Task SendNotificationAsync_CallsProxyWithCorrectPayload()
    {
        // Arrange
        var context = CreateTestContext();
        ApprisePayload? capturedPayload = null;

        _apiProxy.SendNotification(Arg.Any<ApprisePayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<ApprisePayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Title.ShouldBe(context.Title);
        capturedPayload.Body.ShouldContain(context.Description);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesDataInBody()
    {
        // Arrange
        var context = CreateTestContext();
        context.Data["TestKey"] = "TestValue";
        context.Data["AnotherKey"] = "AnotherValue";

        ApprisePayload? capturedPayload = null;

        _apiProxy.SendNotification(Arg.Any<ApprisePayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<ApprisePayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Body.ShouldContain("TestKey: TestValue");
        capturedPayload.Body.ShouldContain("AnotherKey: AnotherValue");
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

        _apiProxy.SendNotification(Arg.Any<ApprisePayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<ApprisePayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Type.ShouldBe(expectedType);
    }

    [Fact]
    public async Task SendNotificationAsync_IncludesTagsFromConfig()
    {
        // Arrange
        var context = CreateTestContext();
        ApprisePayload? capturedPayload = null;

        _apiProxy.SendNotification(Arg.Any<ApprisePayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<ApprisePayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Tags.ShouldBe("tag1,tag2");
    }

    [Fact]
    public async Task SendNotificationAsync_WhenProxyThrows_PropagatesException()
    {
        // Arrange
        var context = CreateTestContext();

        _apiProxy.SendNotification(Arg.Any<ApprisePayload>(), _config)
            .ThrowsAsync(new Exception("Proxy error"));

        // Act & Assert
        await Should.ThrowAsync<Exception>(() => _provider.SendNotificationAsync(context));
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

        _apiProxy.SendNotification(Arg.Any<ApprisePayload>(), _config)
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedPayload = ci.ArgAt<ApprisePayload>(0));

        // Act
        await _provider.SendNotificationAsync(context);

        // Assert
        capturedPayload.ShouldNotBeNull();
        capturedPayload.Body.ShouldContain("Test Description");
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

        var apiProxy = Substitute.For<IAppriseProxy>();
        var cliProxy = Substitute.For<IAppriseCliProxy>();

        var provider = new AppriseProvider(
            "TestAppriseCli",
            NotificationProviderType.Apprise,
            cliConfig,
            apiProxy,
            cliProxy);

        var context = CreateTestContext();

        cliProxy.SendNotification(Arg.Any<ApprisePayload>(), cliConfig)
            .Returns(Task.CompletedTask);

        // Act
        await provider.SendNotificationAsync(context);

        // Assert
        await cliProxy.Received(1).SendNotification(Arg.Any<ApprisePayload>(), cliConfig);
        await apiProxy.DidNotReceive().SendNotification(Arg.Any<ApprisePayload>(), Arg.Any<AppriseConfig>());
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
