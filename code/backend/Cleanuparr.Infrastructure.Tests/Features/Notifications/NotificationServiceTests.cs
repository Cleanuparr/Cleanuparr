using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NotificationServiceTests
{
    private readonly Mock<ILogger<NotificationService>> _loggerMock;
    private readonly Mock<INotificationConfigurationService> _configServiceMock;
    private readonly Mock<INotificationProviderFactory> _providerFactoryMock;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _loggerMock = new Mock<ILogger<NotificationService>>();
        _configServiceMock = new Mock<INotificationConfigurationService>();
        _providerFactoryMock = new Mock<INotificationProviderFactory>();

        _service = new NotificationService(
            _loggerMock.Object,
            _configServiceMock.Object,
            _providerFactoryMock.Object);
    }

    #region SendNotificationAsync Tests

    [Fact]
    public async Task SendNotificationAsync_NoProviders_DoesNotSendNotifications()
    {
        // Arrange
        var eventType = NotificationEventType.QueueItemDeleted;
        var context = CreateTestContext();

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(eventType))
            .ReturnsAsync(new List<NotificationProviderDto>());

        // Act
        await _service.SendNotificationAsync(eventType, context);

        // Assert
        _providerFactoryMock.Verify(f => f.CreateProvider(It.IsAny<NotificationProviderDto>()), Times.Never);
    }

    [Fact]
    public async Task SendNotificationAsync_WithProvider_SendsNotification()
    {
        // Arrange
        var eventType = NotificationEventType.DownloadCleaned;
        var context = CreateTestContext();
        var providerConfig = CreateProviderConfig("TestProvider");

        var providerMock = new Mock<INotificationProvider>();
        providerMock.SetupGet(p => p.Name).Returns("TestProvider");

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(eventType))
            .ReturnsAsync(new List<NotificationProviderDto> { providerConfig });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerConfig))
            .Returns(providerMock.Object);

        // Act
        await _service.SendNotificationAsync(eventType, context);

        // Assert
        providerMock.Verify(p => p.SendNotificationAsync(context), Times.Once);
    }

    [Fact]
    public async Task SendNotificationAsync_WithMultipleProviders_SendsToAll()
    {
        // Arrange
        var eventType = NotificationEventType.StalledStrike;
        var context = CreateTestContext();
        var provider1Config = CreateProviderConfig("Provider1");
        var provider2Config = CreateProviderConfig("Provider2");

        var provider1Mock = new Mock<INotificationProvider>();
        provider1Mock.SetupGet(p => p.Name).Returns("Provider1");

        var provider2Mock = new Mock<INotificationProvider>();
        provider2Mock.SetupGet(p => p.Name).Returns("Provider2");

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(eventType))
            .ReturnsAsync(new List<NotificationProviderDto> { provider1Config, provider2Config });
        _providerFactoryMock.Setup(f => f.CreateProvider(provider1Config))
            .Returns(provider1Mock.Object);
        _providerFactoryMock.Setup(f => f.CreateProvider(provider2Config))
            .Returns(provider2Mock.Object);

        // Act
        await _service.SendNotificationAsync(eventType, context);

        // Assert
        provider1Mock.Verify(p => p.SendNotificationAsync(context), Times.Once);
        provider2Mock.Verify(p => p.SendNotificationAsync(context), Times.Once);
    }

    [Fact]
    public async Task SendNotificationAsync_OneProviderFails_OthersStillExecute()
    {
        // Arrange
        var eventType = NotificationEventType.CategoryChanged;
        var context = CreateTestContext();
        var failingProviderConfig = CreateProviderConfig("FailingProvider");
        var successProviderConfig = CreateProviderConfig("SuccessProvider");

        var failingProviderMock = new Mock<INotificationProvider>();
        failingProviderMock.SetupGet(p => p.Name).Returns("FailingProvider");
        failingProviderMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()))
            .ThrowsAsync(new Exception("Provider failed"));

        var successProviderMock = new Mock<INotificationProvider>();
        successProviderMock.SetupGet(p => p.Name).Returns("SuccessProvider");

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(eventType))
            .ReturnsAsync(new List<NotificationProviderDto> { failingProviderConfig, successProviderConfig });
        _providerFactoryMock.Setup(f => f.CreateProvider(failingProviderConfig))
            .Returns(failingProviderMock.Object);
        _providerFactoryMock.Setup(f => f.CreateProvider(successProviderConfig))
            .Returns(successProviderMock.Object);

        // Act
        await _service.SendNotificationAsync(eventType, context);

        // Assert - both providers should have been called
        failingProviderMock.Verify(p => p.SendNotificationAsync(context), Times.Once);
        successProviderMock.Verify(p => p.SendNotificationAsync(context), Times.Once);
    }

    [Fact]
    public async Task SendNotificationAsync_ProviderFails_LogsWarning()
    {
        // Arrange
        var eventType = NotificationEventType.QueueItemDeleted;
        var context = CreateTestContext();
        var providerConfig = CreateProviderConfig("FailingProvider");

        var providerMock = new Mock<INotificationProvider>();
        providerMock.SetupGet(p => p.Name).Returns("FailingProvider");
        providerMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()))
            .ThrowsAsync(new Exception("Provider failed"));

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(eventType))
            .ReturnsAsync(new List<NotificationProviderDto> { providerConfig });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerConfig))
            .Returns(providerMock.Object);

        // Act
        await _service.SendNotificationAsync(eventType, context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotificationAsync_ConfigServiceThrows_LogsError()
    {
        // Arrange
        var eventType = NotificationEventType.SlowSpeedStrike;
        var context = CreateTestContext();

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(eventType))
            .ThrowsAsync(new Exception("Config service failed"));

        // Act
        await _service.SendNotificationAsync(eventType, context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send notifications")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SendTestNotificationAsync Tests

    [Fact]
    public async Task SendTestNotificationAsync_SendsTestContext()
    {
        // Arrange
        var providerConfig = CreateProviderConfig("TestProvider");
        var providerMock = new Mock<INotificationProvider>();
        providerMock.SetupGet(p => p.Name).Returns("TestProvider");

        _providerFactoryMock.Setup(f => f.CreateProvider(providerConfig))
            .Returns(providerMock.Object);

        // Act
        await _service.SendTestNotificationAsync(providerConfig);

        // Assert
        providerMock.Verify(p => p.SendNotificationAsync(It.Is<NotificationContext>(c =>
            c.EventType == NotificationEventType.Test &&
            c.Title == "Test Notification from Cleanuparr" &&
            c.Description.Contains("test notification") &&
            c.Severity == EventSeverity.Information &&
            c.Data != null &&
            c.Data.ContainsKey("Test time") &&
            c.Data.ContainsKey("Provider type")
        )), Times.Once);
    }

    [Fact]
    public async Task SendTestNotificationAsync_Success_LogsInformation()
    {
        // Arrange
        var providerConfig = CreateProviderConfig("TestProvider");
        var providerMock = new Mock<INotificationProvider>();
        providerMock.SetupGet(p => p.Name).Returns("TestProvider");

        _providerFactoryMock.Setup(f => f.CreateProvider(providerConfig))
            .Returns(providerMock.Object);

        // Act
        await _service.SendTestNotificationAsync(providerConfig);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Test notification sent successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTestNotificationAsync_ProviderFails_ThrowsException()
    {
        // Arrange
        var providerConfig = CreateProviderConfig("FailingProvider");
        var providerMock = new Mock<INotificationProvider>();
        providerMock.SetupGet(p => p.Name).Returns("FailingProvider");
        providerMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()))
            .ThrowsAsync(new Exception("Test notification failed"));

        _providerFactoryMock.Setup(f => f.CreateProvider(providerConfig))
            .Returns(providerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _service.SendTestNotificationAsync(providerConfig));
    }

    [Fact]
    public async Task SendTestNotificationAsync_ProviderFails_LogsError()
    {
        // Arrange
        var providerConfig = CreateProviderConfig("FailingProvider");
        var providerMock = new Mock<INotificationProvider>();
        providerMock.SetupGet(p => p.Name).Returns("FailingProvider");
        providerMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()))
            .ThrowsAsync(new Exception("Test notification failed"));

        _providerFactoryMock.Setup(f => f.CreateProvider(providerConfig))
            .Returns(providerMock.Object);

        // Act
        try
        {
            await _service.SendTestNotificationAsync(providerConfig);
        }
        catch
        {
            // Expected
        }

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send test notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTestNotificationAsync_IncludesProviderTypeInData()
    {
        // Arrange
        var providerConfig = new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = "TestNtfyProvider",
            Type = NotificationProviderType.Ntfy,
            IsEnabled = true
        };

        var providerMock = new Mock<INotificationProvider>();
        providerMock.SetupGet(p => p.Name).Returns("TestNtfyProvider");

        _providerFactoryMock.Setup(f => f.CreateProvider(providerConfig))
            .Returns(providerMock.Object);

        // Act
        await _service.SendTestNotificationAsync(providerConfig);

        // Assert
        providerMock.Verify(p => p.SendNotificationAsync(It.Is<NotificationContext>(c =>
            c.Data["Provider type"] == "Ntfy"
        )), Times.Once);
    }

    #endregion

    #region Helper Methods

    private static NotificationContext CreateTestContext()
    {
        return new NotificationContext
        {
            EventType = NotificationEventType.QueueItemDeleted,
            Title = "Test Title",
            Description = "Test Description",
            Severity = EventSeverity.Information,
            Data = new Dictionary<string, string>
            {
                ["Key1"] = "Value1",
                ["Key2"] = "Value2"
            }
        };
    }

    private static NotificationProviderDto CreateProviderConfig(string name)
    {
        return new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = NotificationProviderType.Apprise,
            IsEnabled = true
        };
    }

    #endregion
}
