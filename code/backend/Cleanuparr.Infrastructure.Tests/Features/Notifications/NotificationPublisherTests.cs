using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NotificationPublisherTests
{
    private readonly Mock<ILogger<NotificationPublisher>> _loggerMock;
    private readonly Mock<IDryRunInterceptor> _dryRunInterceptorMock;
    private readonly Mock<INotificationConfigurationService> _configServiceMock;
    private readonly Mock<INotificationProviderFactory> _providerFactoryMock;
    private readonly NotificationPublisher _publisher;

    public NotificationPublisherTests()
    {
        _loggerMock = new Mock<ILogger<NotificationPublisher>>();
        _dryRunInterceptorMock = new Mock<IDryRunInterceptor>();
        _configServiceMock = new Mock<INotificationConfigurationService>();
        _providerFactoryMock = new Mock<INotificationProviderFactory>();

        // Setup dry run interceptor to call through
        _dryRunInterceptorMock
            .Setup(d => d.InterceptAsync(It.IsAny<Delegate>(), It.IsAny<object[]>()))
            .Returns<Delegate, object[]>(async (action, parameters) =>
            {
                if (action is Func<(NotificationEventType, NotificationContext), Task> func && parameters.Length > 0)
                {
                    var param = ((NotificationEventType, NotificationContext))parameters[0];
                    await func(param);
                }
            });

        _publisher = new NotificationPublisher(
            _loggerMock.Object,
            _dryRunInterceptorMock.Object,
            _configServiceMock.Object,
            _providerFactoryMock.Object);
    }

    private void SetupContext(InstanceType instanceType = InstanceType.Sonarr)
    {
        var record = new QueueRecord
        {
            Id = 1,
            Title = "Test Show",
            DownloadId = "ABCD1234",
            Status = "Downloading",
            Protocol = "torrent"
        };

        ContextProvider.Set(nameof(QueueRecord), record);
        ContextProvider.Set(nameof(InstanceType), instanceType);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, new Uri("http://sonarr.local"));
        ContextProvider.Set(ContextProvider.Keys.Version, 1f);
    }

    private void SetupDownloadCleanerContext()
    {
        ContextProvider.Set(ContextProvider.Keys.DownloadName, "Test Download");
        ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, new Uri("http://downloadclient.local"));
        ContextProvider.Set(ContextProvider.Keys.Hash, "HASH123");
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsAllDependencies()
    {
        // Assert
        Assert.NotNull(_publisher);
    }

    #endregion

    #region NotifyStrike Tests

    [Fact]
    public async Task NotifyStrike_WithStalledStrike_SendsNotification()
    {
        // Arrange
        SetupContext();
        var rule = new StallRule { Name = "Test Rule" };
        ContextProvider.Set<QueueRule>(rule);

        var providerDto = CreateProviderDto();
        var providerMock = new Mock<INotificationProvider>();

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(NotificationEventType.StalledStrike))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto))
            .Returns(providerMock.Object);

        // Act
        await _publisher.NotifyStrike(StrikeType.Stalled, 1);

        // Assert
        providerMock.Verify(p => p.SendNotificationAsync(It.Is<NotificationContext>(
            c => c.EventType == NotificationEventType.StalledStrike &&
                 c.Data.ContainsKey("Strike type") &&
                 c.Data["Strike type"] == "Stalled")), Times.Once);
    }

    [Fact]
    public async Task NotifyStrike_WithFailedImportStrike_MapsToCorrectEventType()
    {
        // Arrange
        SetupContext();

        var providerDto = CreateProviderDto();
        var providerMock = new Mock<INotificationProvider>();

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(NotificationEventType.FailedImportStrike))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto))
            .Returns(providerMock.Object);

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 2);

        // Assert
        providerMock.Verify(p => p.SendNotificationAsync(It.Is<NotificationContext>(
            c => c.EventType == NotificationEventType.FailedImportStrike &&
                 c.Data["Strike count"] == "2")), Times.Once);
    }

    [Theory]
    [InlineData(StrikeType.Stalled, NotificationEventType.StalledStrike)]
    [InlineData(StrikeType.DownloadingMetadata, NotificationEventType.StalledStrike)]
    [InlineData(StrikeType.FailedImport, NotificationEventType.FailedImportStrike)]
    [InlineData(StrikeType.SlowSpeed, NotificationEventType.SlowSpeedStrike)]
    [InlineData(StrikeType.SlowTime, NotificationEventType.SlowTimeStrike)]
    public async Task NotifyStrike_MapsStrikeTypeToCorrectEventType(StrikeType strikeType, NotificationEventType expectedEventType)
    {
        // Arrange
        SetupContext();
        if (strikeType is StrikeType.Stalled or StrikeType.SlowSpeed or StrikeType.SlowTime)
        {
            var rule = new StallRule { Name = "Test Rule" };
            ContextProvider.Set<QueueRule>(rule);
        }

        var providerDto = CreateProviderDto();
        var providerMock = new Mock<INotificationProvider>();

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(expectedEventType))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto))
            .Returns(providerMock.Object);

        // Act
        await _publisher.NotifyStrike(strikeType, 1);

        // Assert
        _configServiceMock.Verify(c => c.GetProvidersForEventAsync(expectedEventType), Times.Once);
    }

    [Fact]
    public async Task NotifyStrike_WhenNoProviders_DoesNotThrow()
    {
        // Arrange
        SetupContext();
        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .ReturnsAsync(new List<NotificationProviderDto>());

        // Act & Assert - Should not throw
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);
    }

    [Fact]
    public async Task NotifyStrike_WhenProviderThrows_LogsWarningAndContinues()
    {
        // Arrange
        SetupContext();

        var providerDto = CreateProviderDto();
        var providerMock = new Mock<INotificationProvider>();
        providerMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()))
            .ThrowsAsync(new Exception("Provider failed"));

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto))
            .Returns(providerMock.Object);

        // Act - Should not throw
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

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
    public async Task NotifyStrike_WithoutExternalUrl_UsesInternalUrlInNotification()
    {
        // Arrange
        SetupContext();

        var providerDto = CreateProviderDto();
        var providerMock = new Mock<INotificationProvider>();

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(NotificationEventType.FailedImportStrike))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto))
            .Returns(providerMock.Object);

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert
        providerMock.Verify(p => p.SendNotificationAsync(It.Is<NotificationContext>(
            c => c.Data["Url"] == "http://sonarr.local/")), Times.Once);
    }

    #endregion

    #region NotifyQueueItemDeleted Tests

    [Fact]
    public async Task NotifyQueueItemDeleted_SendsNotificationWithCorrectContext()
    {
        // Arrange
        SetupContext();

        var providerDto = CreateProviderDto();
        var providerMock = new Mock<INotificationProvider>();

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(NotificationEventType.QueueItemDeleted))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto))
            .Returns(providerMock.Object);

        // Act
        await _publisher.NotifyQueueItemDeleted(true, DeleteReason.Stalled);

        // Assert
        providerMock.Verify(p => p.SendNotificationAsync(It.Is<NotificationContext>(
            c => c.EventType == NotificationEventType.QueueItemDeleted &&
                 c.Data["Reason"] == "Stalled" &&
                 c.Data["Removed from client?"] == "True" &&
                 c.Severity == EventSeverity.Important)), Times.Once);
    }

    [Fact]
    public async Task NotifyQueueItemDeleted_WhenRemoveFromClientFalse_ReflectsInContext()
    {
        // Arrange
        SetupContext();

        var providerDto = CreateProviderDto();
        var providerMock = new Mock<INotificationProvider>();

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(NotificationEventType.QueueItemDeleted))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto))
            .Returns(providerMock.Object);

        // Act
        await _publisher.NotifyQueueItemDeleted(false, DeleteReason.MalwareFileFound);

        // Assert
        providerMock.Verify(p => p.SendNotificationAsync(It.Is<NotificationContext>(
            c => c.Data["Removed from client?"] == "False" &&
                 c.Data["Reason"] == "MalwareFileFound")), Times.Once);
    }

    #endregion

    #region NotifyDownloadCleaned Tests

    [Fact]
    public async Task NotifyDownloadCleaned_SendsNotificationWithCorrectContext()
    {
        // Arrange
        SetupDownloadCleanerContext();

        var providerDto = CreateProviderDto();
        var providerMock = new Mock<INotificationProvider>();

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(NotificationEventType.DownloadCleaned))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto))
            .Returns(providerMock.Object);

        // Act
        await _publisher.NotifyDownloadCleaned(2.5, TimeSpan.FromHours(48), "movies", CleanReason.MaxRatioReached);

        // Assert
        providerMock.Verify(p => p.SendNotificationAsync(It.Is<NotificationContext>(
            c => c.EventType == NotificationEventType.DownloadCleaned &&
                 c.Description == "Test Download" &&
                 c.Data["Category"] == "movies" &&
                 c.Data["Ratio"] == "2.5" &&
                 c.Data["Seeding hours"] == "48")), Times.Once);
    }

    [Fact]
    public async Task NotifyDownloadCleaned_WithSeedingTime_RoundsToWholeHours()
    {
        // Arrange
        SetupDownloadCleanerContext();

        var providerDto = CreateProviderDto();
        var providerMock = new Mock<INotificationProvider>();
        NotificationContext? capturedContext = null;

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(NotificationEventType.DownloadCleaned))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto))
            .Returns(providerMock.Object);
        providerMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()))
            .Callback<NotificationContext>(c => capturedContext = c)
            .Returns(Task.CompletedTask);

        // Act
        await _publisher.NotifyDownloadCleaned(1.0, TimeSpan.FromHours(24.7), "tv", CleanReason.MaxSeedTimeReached);

        // Assert
        Assert.NotNull(capturedContext);
        Assert.Equal("25", capturedContext.Data["Seeding hours"]); // Rounds to 25
    }

    [Fact]
    public async Task NotifyDownloadCleaned_WithDownloadClientUrl_IncludesUrlInNotification()
    {
        // Arrange
        SetupDownloadCleanerContext();
        ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, new Uri("https://qbit.external.com"));

        var providerDto = CreateProviderDto();
        var providerMock = new Mock<INotificationProvider>();

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(NotificationEventType.DownloadCleaned))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto))
            .Returns(providerMock.Object);

        // Act
        await _publisher.NotifyDownloadCleaned(2.5, TimeSpan.FromHours(48), "movies", CleanReason.MaxRatioReached);

        // Assert
        providerMock.Verify(p => p.SendNotificationAsync(It.Is<NotificationContext>(
            c => c.Data.ContainsKey("Url") &&
                 c.Data["Url"] == "https://qbit.external.com/")), Times.Once);
    }

    #endregion

    #region NotifyCategoryChanged Tests

    [Fact]
    public async Task NotifyCategoryChanged_WhenNotTag_IncludesOldAndNewCategory()
    {
        // Arrange
        SetupDownloadCleanerContext();

        var providerDto = CreateProviderDto();
        var providerMock = new Mock<INotificationProvider>();

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(NotificationEventType.CategoryChanged))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto))
            .Returns(providerMock.Object);

        // Act
        await _publisher.NotifyCategoryChanged("tv-sonarr", "seeding", false);

        // Assert
        providerMock.Verify(p => p.SendNotificationAsync(It.Is<NotificationContext>(
            c => c.EventType == NotificationEventType.CategoryChanged &&
                 c.Title == "Category changed" &&
                 c.Data["Old category"] == "tv-sonarr" &&
                 c.Data["New category"] == "seeding")), Times.Once);
    }

    [Fact]
    public async Task NotifyCategoryChanged_WhenIsTag_IncludesOnlyTag()
    {
        // Arrange
        SetupDownloadCleanerContext();

        var providerDto = CreateProviderDto();
        var providerMock = new Mock<INotificationProvider>();
        NotificationContext? capturedContext = null;

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(NotificationEventType.CategoryChanged))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto))
            .Returns(providerMock.Object);
        providerMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()))
            .Callback<NotificationContext>(c => capturedContext = c)
            .Returns(Task.CompletedTask);

        // Act
        await _publisher.NotifyCategoryChanged("", "seeded", true);

        // Assert
        Assert.NotNull(capturedContext);
        Assert.Equal("Tag added", capturedContext.Title);
        Assert.True(capturedContext.Data.ContainsKey("Tag"));
        Assert.Equal("seeded", capturedContext.Data["Tag"]);
        Assert.False(capturedContext.Data.ContainsKey("Old category"));
        Assert.False(capturedContext.Data.ContainsKey("New category"));
    }

    [Fact]
    public async Task NotifyCategoryChanged_SetsSeverityToInformation()
    {
        // Arrange
        SetupDownloadCleanerContext();

        var providerDto = CreateProviderDto();
        var providerMock = new Mock<INotificationProvider>();

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(NotificationEventType.CategoryChanged))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto))
            .Returns(providerMock.Object);

        // Act
        await _publisher.NotifyCategoryChanged("old", "new", false);

        // Assert
        providerMock.Verify(p => p.SendNotificationAsync(It.Is<NotificationContext>(
            c => c.Severity == EventSeverity.Information)), Times.Once);
    }

    #endregion

    #region SendNotificationAsync Tests (through notify methods)

    [Fact]
    public async Task SendNotificationAsync_WhenMultipleProviders_SendsToAll()
    {
        // Arrange
        SetupContext();

        var providerDto1 = CreateProviderDto("Provider1");
        var providerDto2 = CreateProviderDto("Provider2");
        var providerMock1 = new Mock<INotificationProvider>();
        var providerMock2 = new Mock<INotificationProvider>();

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(NotificationEventType.FailedImportStrike))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto1, providerDto2 });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto1))
            .Returns(providerMock1.Object);
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto2))
            .Returns(providerMock2.Object);

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert
        providerMock1.Verify(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()), Times.Once);
        providerMock2.Verify(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()), Times.Once);
    }

    [Fact]
    public async Task SendNotificationAsync_WhenOneProviderFails_OthersStillSend()
    {
        // Arrange
        SetupContext();

        var providerDto1 = CreateProviderDto("Provider1");
        var providerDto2 = CreateProviderDto("Provider2");
        var providerMock1 = new Mock<INotificationProvider>();
        var providerMock2 = new Mock<INotificationProvider>();

        providerMock1.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()))
            .ThrowsAsync(new Exception("Failed"));

        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(NotificationEventType.FailedImportStrike))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto1, providerDto2 });
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto1))
            .Returns(providerMock1.Object);
        _providerFactoryMock.Setup(f => f.CreateProvider(providerDto2))
            .Returns(providerMock2.Object);

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert - Provider2 should still be called
        providerMock2.Verify(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()), Times.Once);
    }

    [Fact]
    public async Task SendNotificationAsync_UsesDryRunInterceptor()
    {
        // Arrange
        SetupContext();
        _configServiceMock.Setup(c => c.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .ReturnsAsync(new List<NotificationProviderDto>());

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert
        _dryRunInterceptorMock.Verify(d => d.InterceptAsync(
            It.IsAny<Func<(NotificationEventType, NotificationContext), Task>>(),
            It.IsAny<(NotificationEventType, NotificationContext)>()), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task NotifyStrike_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        // Setup dry run interceptor to throw when called
        _dryRunInterceptorMock
            .Setup(d => d.InterceptAsync(It.IsAny<Delegate>(), It.IsAny<object[]>()))
            .ThrowsAsync(new Exception("Interceptor failed"));

        SetupContext();

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed to notify strike")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyQueueItemDeleted_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        _dryRunInterceptorMock
            .Setup(d => d.InterceptAsync(It.IsAny<Delegate>(), It.IsAny<object[]>()))
            .ThrowsAsync(new Exception("Error"));

        SetupContext();

        // Act
        await _publisher.NotifyQueueItemDeleted(true, DeleteReason.Stalled);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to notify queue item deleted")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyDownloadCleaned_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        _dryRunInterceptorMock
            .Setup(d => d.InterceptAsync(It.IsAny<Delegate>(), It.IsAny<object[]>()))
            .ThrowsAsync(new Exception("Error"));

        SetupDownloadCleanerContext();

        // Act
        await _publisher.NotifyDownloadCleaned(1.0, TimeSpan.FromHours(1), "test", CleanReason.MaxRatioReached);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to notify download cleaned")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyCategoryChanged_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        _dryRunInterceptorMock
            .Setup(d => d.InterceptAsync(It.IsAny<Delegate>(), It.IsAny<object[]>()))
            .ThrowsAsync(new Exception("Error"));

        SetupDownloadCleanerContext();

        // Act
        await _publisher.NotifyCategoryChanged("old", "new", false);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to notify category changed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private static NotificationProviderDto CreateProviderDto(string name = "TestProvider")
    {
        return new NotificationProviderDto
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = NotificationProviderType.Notifiarr,
            IsEnabled = true,
            Events = new NotificationEventFlags
            {
                OnFailedImportStrike = true,
                OnStalledStrike = true,
                OnSlowStrike = true,
                OnQueueItemDeleted = true,
                OnDownloadCleaned = true,
                OnCategoryChanged = true
            },
            Configuration = new { ApiKey = "test", ChannelId = "123" }
        };
    }

    #endregion
}
