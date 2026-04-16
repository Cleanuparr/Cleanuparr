using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NotificationServiceTests
{
    private readonly ILogger<NotificationService> _logger;
    private readonly INotificationConfigurationService _configService;
    private readonly INotificationProviderFactory _providerFactory;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _logger = Substitute.For<ILogger<NotificationService>>();
        _configService = Substitute.For<INotificationConfigurationService>();
        _providerFactory = Substitute.For<INotificationProviderFactory>();

        _service = new NotificationService(
            _logger,
            _configService,
            _providerFactory);
    }

    #region SendNotificationAsync Tests

    [Fact]
    public async Task SendNotificationAsync_NoProviders_DoesNotSendNotifications()
    {
        // Arrange
        var eventType = NotificationEventType.QueueItemDeleted;
        var context = CreateTestContext();

        _configService.GetProvidersForEventAsync(eventType)
            .Returns(new List<NotificationProviderDto>());

        // Act
        await _service.SendNotificationAsync(eventType, context);

        // Assert
        _providerFactory.DidNotReceive().CreateProvider(Arg.Any<NotificationProviderDto>());
    }

    [Fact]
    public async Task SendNotificationAsync_WithProvider_SendsNotification()
    {
        // Arrange
        var eventType = NotificationEventType.DownloadCleaned;
        var context = CreateTestContext();
        var providerConfig = CreateProviderConfig("TestProvider");

        var provider = Substitute.For<INotificationProvider>();
        provider.Name.Returns("TestProvider");

        _configService.GetProvidersForEventAsync(eventType)
            .Returns(new List<NotificationProviderDto> { providerConfig });
        _providerFactory.CreateProvider(providerConfig)
            .Returns(provider);

        // Act
        await _service.SendNotificationAsync(eventType, context);

        // Assert
        await provider.Received(1).SendNotificationAsync(context);
    }

    [Fact]
    public async Task SendNotificationAsync_WithMultipleProviders_SendsToAll()
    {
        // Arrange
        var eventType = NotificationEventType.StalledStrike;
        var context = CreateTestContext();
        var provider1Config = CreateProviderConfig("Provider1");
        var provider2Config = CreateProviderConfig("Provider2");

        var provider1 = Substitute.For<INotificationProvider>();
        provider1.Name.Returns("Provider1");

        var provider2 = Substitute.For<INotificationProvider>();
        provider2.Name.Returns("Provider2");

        _configService.GetProvidersForEventAsync(eventType)
            .Returns(new List<NotificationProviderDto> { provider1Config, provider2Config });
        _providerFactory.CreateProvider(provider1Config)
            .Returns(provider1);
        _providerFactory.CreateProvider(provider2Config)
            .Returns(provider2);

        // Act
        await _service.SendNotificationAsync(eventType, context);

        // Assert
        await provider1.Received(1).SendNotificationAsync(context);
        await provider2.Received(1).SendNotificationAsync(context);
    }

    [Fact]
    public async Task SendNotificationAsync_OneProviderFails_OthersStillExecute()
    {
        // Arrange
        var eventType = NotificationEventType.CategoryChanged;
        var context = CreateTestContext();
        var failingProviderConfig = CreateProviderConfig("FailingProvider");
        var successProviderConfig = CreateProviderConfig("SuccessProvider");

        var failingProvider = Substitute.For<INotificationProvider>();
        failingProvider.Name.Returns("FailingProvider");
        failingProvider.SendNotificationAsync(Arg.Any<NotificationContext>())
            .ThrowsAsync(new Exception("Provider failed"));

        var successProvider = Substitute.For<INotificationProvider>();
        successProvider.Name.Returns("SuccessProvider");

        _configService.GetProvidersForEventAsync(eventType)
            .Returns(new List<NotificationProviderDto> { failingProviderConfig, successProviderConfig });
        _providerFactory.CreateProvider(failingProviderConfig)
            .Returns(failingProvider);
        _providerFactory.CreateProvider(successProviderConfig)
            .Returns(successProvider);

        // Act
        await _service.SendNotificationAsync(eventType, context);

        // Assert - both providers should have been called
        await failingProvider.Received(1).SendNotificationAsync(context);
        await successProvider.Received(1).SendNotificationAsync(context);
    }

    [Fact]
    public async Task SendNotificationAsync_ProviderFails_LogsWarning()
    {
        // Arrange
        var eventType = NotificationEventType.QueueItemDeleted;
        var context = CreateTestContext();
        var providerConfig = CreateProviderConfig("FailingProvider");

        var provider = Substitute.For<INotificationProvider>();
        provider.Name.Returns("FailingProvider");
        provider.SendNotificationAsync(Arg.Any<NotificationContext>())
            .ThrowsAsync(new Exception("Provider failed"));

        _configService.GetProvidersForEventAsync(eventType)
            .Returns(new List<NotificationProviderDto> { providerConfig });
        _providerFactory.CreateProvider(providerConfig)
            .Returns(provider);

        // Act
        await _service.SendNotificationAsync(eventType, context);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Warning, "Failed to send notification");
    }

    [Fact]
    public async Task SendNotificationAsync_ConfigServiceThrows_LogsError()
    {
        // Arrange
        var eventType = NotificationEventType.SlowSpeedStrike;
        var context = CreateTestContext();

        _configService.GetProvidersForEventAsync(eventType)
            .ThrowsAsync(new Exception("Config service failed"));

        // Act
        await _service.SendNotificationAsync(eventType, context);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "Failed to send notifications");
    }

    #endregion

    #region SendTestNotificationAsync Tests

    [Fact]
    public async Task SendTestNotificationAsync_SendsTestContext()
    {
        // Arrange
        var providerConfig = CreateProviderConfig("TestProvider");
        var provider = Substitute.For<INotificationProvider>();
        provider.Name.Returns("TestProvider");

        _providerFactory.CreateProvider(providerConfig)
            .Returns(provider);

        // Act
        await _service.SendTestNotificationAsync(providerConfig);

        // Assert
        await provider.Received(1).SendNotificationAsync(Arg.Is<NotificationContext>(c =>
            c.EventType == NotificationEventType.Test &&
            c.Title == "Test Notification from Cleanuparr" &&
            c.Description.Contains("test notification") &&
            c.Severity == EventSeverity.Information &&
            c.Data != null &&
            c.Data.ContainsKey("Test time") &&
            c.Data.ContainsKey("Provider type")
        ));
    }

    [Fact]
    public async Task SendTestNotificationAsync_Success_LogsInformation()
    {
        // Arrange
        var providerConfig = CreateProviderConfig("TestProvider");
        var provider = Substitute.For<INotificationProvider>();
        provider.Name.Returns("TestProvider");

        _providerFactory.CreateProvider(providerConfig)
            .Returns(provider);

        // Act
        await _service.SendTestNotificationAsync(providerConfig);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Information, "Test notification sent successfully");
    }

    [Fact]
    public async Task SendTestNotificationAsync_ProviderFails_ThrowsException()
    {
        // Arrange
        var providerConfig = CreateProviderConfig("FailingProvider");
        var provider = Substitute.For<INotificationProvider>();
        provider.Name.Returns("FailingProvider");
        provider.SendNotificationAsync(Arg.Any<NotificationContext>())
            .ThrowsAsync(new Exception("Test notification failed"));

        _providerFactory.CreateProvider(providerConfig)
            .Returns(provider);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _service.SendTestNotificationAsync(providerConfig));
    }

    [Fact]
    public async Task SendTestNotificationAsync_ProviderFails_LogsError()
    {
        // Arrange
        var providerConfig = CreateProviderConfig("FailingProvider");
        var provider = Substitute.For<INotificationProvider>();
        provider.Name.Returns("FailingProvider");
        provider.SendNotificationAsync(Arg.Any<NotificationContext>())
            .ThrowsAsync(new Exception("Test notification failed"));

        _providerFactory.CreateProvider(providerConfig)
            .Returns(provider);

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
        _logger.ReceivedLogContaining(LogLevel.Error, "Failed to send test notification");
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

        var provider = Substitute.For<INotificationProvider>();
        provider.Name.Returns("TestNtfyProvider");

        _providerFactory.CreateProvider(providerConfig)
            .Returns(provider);

        // Act
        await _service.SendTestNotificationAsync(providerConfig);

        // Assert
        await provider.Received(1).SendNotificationAsync(Arg.Is<NotificationContext>(c =>
            c.Data["Provider type"] == "Ntfy"
        ));
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
