using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Consumers;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NotificationConsumerTests
{
    private readonly Mock<ILogger<NotificationService>> _serviceLoggerMock;
    private readonly Mock<INotificationConfigurationService> _configurationServiceMock;
    private readonly Mock<INotificationProviderFactory> _providerFactoryMock;
    private readonly NotificationService _notificationService;

    public NotificationConsumerTests()
    {
        _serviceLoggerMock = new Mock<ILogger<NotificationService>>();
        _configurationServiceMock = new Mock<INotificationConfigurationService>();
        _providerFactoryMock = new Mock<INotificationProviderFactory>();

        _notificationService = new NotificationService(
            _serviceLoggerMock.Object,
            _configurationServiceMock.Object,
            _providerFactoryMock.Object);
    }

    #region Consume Tests - FailedImportStrikeNotification

    [Fact]
    public async Task Consume_FailedImportStrikeNotification_SendsCorrectEventType()
    {
        // Arrange
        var consumer = CreateConsumer<FailedImportStrikeNotification>();
        var notification = new FailedImportStrikeNotification
        {
            Title = "Test Failed Import",
            Description = "Test Description",
            Level = NotificationLevel.Warning,
            InstanceType = InstanceType.Radarr,
            InstanceUrl = new Uri("http://radarr.local"),
            Hash = "TEST123"
        };
        var contextMock = CreateConsumeContextMock(notification);
        NotificationEventType? capturedEventType = null;

        var providerMock = new Mock<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationServiceMock
            .Setup(s => s.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .Callback<NotificationEventType>(e => capturedEventType = e)
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });

        _providerFactoryMock
            .Setup(f => f.CreateProvider(It.IsAny<NotificationProviderDto>()))
            .Returns(providerMock.Object);

        providerMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>())).Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        Assert.Equal(NotificationEventType.FailedImportStrike, capturedEventType);
    }

    #endregion

    #region Consume Tests - StalledStrikeNotification

    [Fact]
    public async Task Consume_StalledStrikeNotification_SendsCorrectEventType()
    {
        // Arrange
        var consumer = CreateConsumer<StalledStrikeNotification>();
        var notification = new StalledStrikeNotification
        {
            Title = "Test Stalled",
            Description = "Stalled Description",
            Level = NotificationLevel.Important,
            InstanceType = InstanceType.Sonarr,
            InstanceUrl = new Uri("http://sonarr.local"),
            Hash = "STALL123"
        };
        var contextMock = CreateConsumeContextMock(notification);
        NotificationEventType? capturedEventType = null;

        var providerMock = new Mock<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationServiceMock
            .Setup(s => s.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .Callback<NotificationEventType>(e => capturedEventType = e)
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });

        _providerFactoryMock
            .Setup(f => f.CreateProvider(It.IsAny<NotificationProviderDto>()))
            .Returns(providerMock.Object);

        providerMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>())).Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        Assert.Equal(NotificationEventType.StalledStrike, capturedEventType);
    }

    #endregion

    #region Consume Tests - SlowSpeedStrikeNotification

    [Fact]
    public async Task Consume_SlowSpeedStrikeNotification_SendsCorrectEventType()
    {
        // Arrange
        var consumer = CreateConsumer<SlowSpeedStrikeNotification>();
        var notification = new SlowSpeedStrikeNotification
        {
            Title = "Slow Speed",
            Description = "Download too slow",
            Level = NotificationLevel.Warning,
            InstanceType = InstanceType.Radarr,
            InstanceUrl = new Uri("http://radarr.local"),
            Hash = "SLOW123"
        };
        var contextMock = CreateConsumeContextMock(notification);
        NotificationEventType? capturedEventType = null;

        var providerMock = new Mock<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationServiceMock
            .Setup(s => s.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .Callback<NotificationEventType>(e => capturedEventType = e)
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });

        _providerFactoryMock
            .Setup(f => f.CreateProvider(It.IsAny<NotificationProviderDto>()))
            .Returns(providerMock.Object);

        providerMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>())).Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        Assert.Equal(NotificationEventType.SlowSpeedStrike, capturedEventType);
    }

    #endregion

    #region Consume Tests - SlowTimeStrikeNotification

    [Fact]
    public async Task Consume_SlowTimeStrikeNotification_SendsCorrectEventType()
    {
        // Arrange
        var consumer = CreateConsumer<SlowTimeStrikeNotification>();
        var notification = new SlowTimeStrikeNotification
        {
            Title = "Slow Time",
            Description = "Download taking too long",
            Level = NotificationLevel.Warning,
            InstanceType = InstanceType.Radarr,
            InstanceUrl = new Uri("http://radarr.local"),
            Hash = "TIME123"
        };
        var contextMock = CreateConsumeContextMock(notification);
        NotificationEventType? capturedEventType = null;

        var providerMock = new Mock<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationServiceMock
            .Setup(s => s.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .Callback<NotificationEventType>(e => capturedEventType = e)
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });

        _providerFactoryMock
            .Setup(f => f.CreateProvider(It.IsAny<NotificationProviderDto>()))
            .Returns(providerMock.Object);

        providerMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>())).Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        Assert.Equal(NotificationEventType.SlowTimeStrike, capturedEventType);
    }

    #endregion

    #region Consume Tests - QueueItemDeletedNotification

    [Fact]
    public async Task Consume_QueueItemDeletedNotification_SendsCorrectEventType()
    {
        // Arrange
        var consumer = CreateConsumer<QueueItemDeletedNotification>();
        var notification = new QueueItemDeletedNotification
        {
            Title = "Item Deleted",
            Description = "Queue item removed",
            Level = NotificationLevel.Important,
            InstanceType = InstanceType.Lidarr,
            InstanceUrl = new Uri("http://lidarr.local"),
            Hash = "DEL123"
        };
        var contextMock = CreateConsumeContextMock(notification);
        NotificationEventType? capturedEventType = null;

        var providerMock = new Mock<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationServiceMock
            .Setup(s => s.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .Callback<NotificationEventType>(e => capturedEventType = e)
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });

        _providerFactoryMock
            .Setup(f => f.CreateProvider(It.IsAny<NotificationProviderDto>()))
            .Returns(providerMock.Object);

        providerMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>())).Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        Assert.Equal(NotificationEventType.QueueItemDeleted, capturedEventType);
    }

    #endregion

    #region Consume Tests - DownloadCleanedNotification

    [Fact]
    public async Task Consume_DownloadCleanedNotification_SendsCorrectEventType()
    {
        // Arrange
        var consumer = CreateConsumer<DownloadCleanedNotification>();
        var notification = new DownloadCleanedNotification
        {
            Title = "Download Cleaned",
            Description = "Old download removed",
            Level = NotificationLevel.Information
        };
        var contextMock = CreateConsumeContextMock(notification);
        NotificationEventType? capturedEventType = null;

        var providerMock = new Mock<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationServiceMock
            .Setup(s => s.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .Callback<NotificationEventType>(e => capturedEventType = e)
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });

        _providerFactoryMock
            .Setup(f => f.CreateProvider(It.IsAny<NotificationProviderDto>()))
            .Returns(providerMock.Object);

        providerMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>())).Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        Assert.Equal(NotificationEventType.DownloadCleaned, capturedEventType);
    }

    #endregion

    #region Consume Tests - CategoryChangedNotification

    [Fact]
    public async Task Consume_CategoryChangedNotification_SendsCorrectEventType()
    {
        // Arrange
        var consumer = CreateConsumer<CategoryChangedNotification>();
        var notification = new CategoryChangedNotification
        {
            Title = "Category Changed",
            Description = "Category updated",
            Level = NotificationLevel.Information
        };
        var contextMock = CreateConsumeContextMock(notification);
        NotificationEventType? capturedEventType = null;

        var providerMock = new Mock<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationServiceMock
            .Setup(s => s.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .Callback<NotificationEventType>(e => capturedEventType = e)
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });

        _providerFactoryMock
            .Setup(f => f.CreateProvider(It.IsAny<NotificationProviderDto>()))
            .Returns(providerMock.Object);

        providerMock.Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>())).Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        Assert.Equal(NotificationEventType.CategoryChanged, capturedEventType);
    }

    #endregion

    #region NotificationContext Conversion Tests

    [Theory]
    [InlineData(NotificationLevel.Information, EventSeverity.Information)]
    [InlineData(NotificationLevel.Warning, EventSeverity.Warning)]
    [InlineData(NotificationLevel.Important, EventSeverity.Important)]
    public async Task Consume_MapsNotificationLevelToSeverity(NotificationLevel level, EventSeverity expectedSeverity)
    {
        // Arrange
        var consumer = CreateConsumer<FailedImportStrikeNotification>();
        var notification = new FailedImportStrikeNotification
        {
            Title = "Test",
            Description = "Test",
            Level = level,
            InstanceType = InstanceType.Radarr,
            InstanceUrl = new Uri("http://radarr.local"),
            Hash = "LEVEL123"
        };
        var contextMock = CreateConsumeContextMock(notification);
        NotificationContext? capturedContext = null;

        var providerMock = new Mock<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationServiceMock
            .Setup(s => s.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });

        _providerFactoryMock
            .Setup(f => f.CreateProvider(It.IsAny<NotificationProviderDto>()))
            .Returns(providerMock.Object);

        providerMock
            .Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()))
            .Callback<NotificationContext>(c => capturedContext = c)
            .Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        Assert.NotNull(capturedContext);
        Assert.Equal(expectedSeverity, capturedContext.Severity);
    }

    [Fact]
    public async Task Consume_ArrNotification_IncludesArrDataInContext()
    {
        // Arrange
        var consumer = CreateConsumer<FailedImportStrikeNotification>();
        var notification = new FailedImportStrikeNotification
        {
            Title = "Test",
            Description = "Test",
            Level = NotificationLevel.Warning,
            InstanceType = InstanceType.Sonarr,
            InstanceUrl = new Uri("http://sonarr.local"),
            Hash = "ABC123",
            Image = new Uri("http://example.com/image.jpg")
        };
        var contextMock = CreateConsumeContextMock(notification);
        NotificationContext? capturedContext = null;

        var providerMock = new Mock<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationServiceMock
            .Setup(s => s.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });

        _providerFactoryMock
            .Setup(f => f.CreateProvider(It.IsAny<NotificationProviderDto>()))
            .Returns(providerMock.Object);

        providerMock
            .Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()))
            .Callback<NotificationContext>(c => capturedContext = c)
            .Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        Assert.NotNull(capturedContext);
        Assert.Equal("Sonarr", capturedContext.Data["Instance type"]);
        Assert.Equal("http://sonarr.local/", capturedContext.Data["Url"]);
        Assert.Equal("ABC123", capturedContext.Data["Hash"]);
        Assert.Equal(new Uri("http://example.com/image.jpg"), capturedContext.Image);
    }

    [Fact]
    public async Task Consume_WithCustomFields_IncludesFieldsInContext()
    {
        // Arrange
        var consumer = CreateConsumer<FailedImportStrikeNotification>();
        var notification = new FailedImportStrikeNotification
        {
            Title = "Test",
            Description = "Test",
            Level = NotificationLevel.Warning,
            InstanceType = InstanceType.Radarr,
            InstanceUrl = new Uri("http://radarr.local"),
            Hash = "XYZ789",
            Fields = new List<NotificationField>
            {
                new() { Key = "CustomKey1", Value = "CustomValue1" },
                new() { Key = "CustomKey2", Value = "CustomValue2" }
            }
        };
        var contextMock = CreateConsumeContextMock(notification);
        NotificationContext? capturedContext = null;

        var providerMock = new Mock<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationServiceMock
            .Setup(s => s.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });

        _providerFactoryMock
            .Setup(f => f.CreateProvider(It.IsAny<NotificationProviderDto>()))
            .Returns(providerMock.Object);

        providerMock
            .Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()))
            .Callback<NotificationContext>(c => capturedContext = c)
            .Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        Assert.NotNull(capturedContext);
        Assert.Equal("CustomValue1", capturedContext.Data["CustomKey1"]);
        Assert.Equal("CustomValue2", capturedContext.Data["CustomKey2"]);
    }

    [Fact]
    public async Task Consume_NonArrNotification_DoesNotIncludeArrData()
    {
        // Arrange
        var consumer = CreateConsumer<DownloadCleanedNotification>();
        var notification = new DownloadCleanedNotification
        {
            Title = "Download Cleaned",
            Description = "Test",
            Level = NotificationLevel.Information
        };
        var contextMock = CreateConsumeContextMock(notification);
        NotificationContext? capturedContext = null;

        var providerMock = new Mock<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationServiceMock
            .Setup(s => s.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .ReturnsAsync(new List<NotificationProviderDto> { providerDto });

        _providerFactoryMock
            .Setup(f => f.CreateProvider(It.IsAny<NotificationProviderDto>()))
            .Returns(providerMock.Object);

        providerMock
            .Setup(p => p.SendNotificationAsync(It.IsAny<NotificationContext>()))
            .Callback<NotificationContext>(c => capturedContext = c)
            .Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        Assert.NotNull(capturedContext);
        Assert.False(capturedContext.Data.ContainsKey("Instance type"));
        Assert.False(capturedContext.Data.ContainsKey("Url"));
        Assert.False(capturedContext.Data.ContainsKey("Hash"));
    }

    #endregion

    #region No Providers Configured Tests

    [Fact]
    public async Task Consume_WhenNoProvidersConfigured_DoesNotSendNotification()
    {
        // Arrange
        var consumer = CreateConsumer<FailedImportStrikeNotification>();
        var notification = new FailedImportStrikeNotification
        {
            Title = "Test",
            Description = "Test",
            Level = NotificationLevel.Warning,
            InstanceType = InstanceType.Radarr,
            InstanceUrl = new Uri("http://radarr.local"),
            Hash = "NOPROV123"
        };
        var contextMock = CreateConsumeContextMock(notification);

        _configurationServiceMock
            .Setup(s => s.GetProvidersForEventAsync(It.IsAny<NotificationEventType>()))
            .ReturnsAsync(new List<NotificationProviderDto>());

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        _providerFactoryMock.Verify(f => f.CreateProvider(It.IsAny<NotificationProviderDto>()), Times.Never);
    }

    #endregion

    #region Helper Methods

    private NotificationConsumer<T> CreateConsumer<T>() where T : Notification
    {
        var loggerMock = new Mock<ILogger<NotificationConsumer<T>>>();
        return new NotificationConsumer<T>(loggerMock.Object, _notificationService);
    }

    private static Mock<ConsumeContext<T>> CreateConsumeContextMock<T>(T message) where T : class
    {
        var mock = new Mock<ConsumeContext<T>>();
        mock.Setup(c => c.Message).Returns(message);
        return mock;
    }

    #endregion
}
