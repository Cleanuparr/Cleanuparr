using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Consumers;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NotificationConsumerTests
{
    private readonly ILogger<NotificationService> _serviceLogger;
    private readonly INotificationConfigurationService _configurationService;
    private readonly INotificationProviderFactory _providerFactory;
    private readonly NotificationService _notificationService;
    private readonly FakeTimeProvider _timeProvider;

    public NotificationConsumerTests()
    {
        _serviceLogger = Substitute.For<ILogger<NotificationService>>();
        _configurationService = Substitute.For<INotificationConfigurationService>();
        _providerFactory = Substitute.For<INotificationProviderFactory>();
        _timeProvider = new FakeTimeProvider();

        _notificationService = new NotificationService(
            _serviceLogger,
            _configurationService,
            _providerFactory);
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
        var context = CreateConsumeContext(notification);
        NotificationEventType? capturedEventType = null;

        var provider = Substitute.For<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationService
            .GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto> { providerDto })
            .AndDoes(ci => capturedEventType = ci.ArgAt<NotificationEventType>(0));

        _providerFactory
            .CreateProvider(Arg.Any<NotificationProviderDto>())
            .Returns(provider);

        provider.SendNotificationAsync(Arg.Any<NotificationContext>()).Returns(Task.CompletedTask);

        // Act
        await ConsumeWithTimeAdvance(consumer, context);

        // Assert
        capturedEventType.ShouldBe(NotificationEventType.FailedImportStrike);
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
        var context = CreateConsumeContext(notification);
        NotificationEventType? capturedEventType = null;

        var provider = Substitute.For<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationService
            .GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto> { providerDto })
            .AndDoes(ci => capturedEventType = ci.ArgAt<NotificationEventType>(0));

        _providerFactory
            .CreateProvider(Arg.Any<NotificationProviderDto>())
            .Returns(provider);

        provider.SendNotificationAsync(Arg.Any<NotificationContext>()).Returns(Task.CompletedTask);

        // Act
        await ConsumeWithTimeAdvance(consumer, context);

        // Assert
        capturedEventType.ShouldBe(NotificationEventType.StalledStrike);
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
        var context = CreateConsumeContext(notification);
        NotificationEventType? capturedEventType = null;

        var provider = Substitute.For<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationService
            .GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto> { providerDto })
            .AndDoes(ci => capturedEventType = ci.ArgAt<NotificationEventType>(0));

        _providerFactory
            .CreateProvider(Arg.Any<NotificationProviderDto>())
            .Returns(provider);

        provider.SendNotificationAsync(Arg.Any<NotificationContext>()).Returns(Task.CompletedTask);

        // Act
        await ConsumeWithTimeAdvance(consumer, context);

        // Assert
        capturedEventType.ShouldBe(NotificationEventType.SlowSpeedStrike);
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
        var context = CreateConsumeContext(notification);
        NotificationEventType? capturedEventType = null;

        var provider = Substitute.For<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationService
            .GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto> { providerDto })
            .AndDoes(ci => capturedEventType = ci.ArgAt<NotificationEventType>(0));

        _providerFactory
            .CreateProvider(Arg.Any<NotificationProviderDto>())
            .Returns(provider);

        provider.SendNotificationAsync(Arg.Any<NotificationContext>()).Returns(Task.CompletedTask);

        // Act
        await ConsumeWithTimeAdvance(consumer, context);

        // Assert
        capturedEventType.ShouldBe(NotificationEventType.SlowTimeStrike);
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
        var context = CreateConsumeContext(notification);
        NotificationEventType? capturedEventType = null;

        var provider = Substitute.For<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationService
            .GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto> { providerDto })
            .AndDoes(ci => capturedEventType = ci.ArgAt<NotificationEventType>(0));

        _providerFactory
            .CreateProvider(Arg.Any<NotificationProviderDto>())
            .Returns(provider);

        provider.SendNotificationAsync(Arg.Any<NotificationContext>()).Returns(Task.CompletedTask);

        // Act
        await ConsumeWithTimeAdvance(consumer, context);

        // Assert
        capturedEventType.ShouldBe(NotificationEventType.QueueItemDeleted);
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
        var context = CreateConsumeContext(notification);
        NotificationEventType? capturedEventType = null;

        var provider = Substitute.For<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationService
            .GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto> { providerDto })
            .AndDoes(ci => capturedEventType = ci.ArgAt<NotificationEventType>(0));

        _providerFactory
            .CreateProvider(Arg.Any<NotificationProviderDto>())
            .Returns(provider);

        provider.SendNotificationAsync(Arg.Any<NotificationContext>()).Returns(Task.CompletedTask);

        // Act
        await ConsumeWithTimeAdvance(consumer, context);

        // Assert
        capturedEventType.ShouldBe(NotificationEventType.DownloadCleaned);
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
        var context = CreateConsumeContext(notification);
        NotificationEventType? capturedEventType = null;

        var provider = Substitute.For<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationService
            .GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto> { providerDto })
            .AndDoes(ci => capturedEventType = ci.ArgAt<NotificationEventType>(0));

        _providerFactory
            .CreateProvider(Arg.Any<NotificationProviderDto>())
            .Returns(provider);

        provider.SendNotificationAsync(Arg.Any<NotificationContext>()).Returns(Task.CompletedTask);

        // Act
        await ConsumeWithTimeAdvance(consumer, context);

        // Assert
        capturedEventType.ShouldBe(NotificationEventType.CategoryChanged);
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
        var context = CreateConsumeContext(notification);
        NotificationContext? capturedContext = null;

        var provider = Substitute.For<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationService
            .GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto> { providerDto });

        _providerFactory
            .CreateProvider(Arg.Any<NotificationProviderDto>())
            .Returns(provider);

        provider
            .SendNotificationAsync(Arg.Any<NotificationContext>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedContext = ci.ArgAt<NotificationContext>(0));

        // Act
        await ConsumeWithTimeAdvance(consumer, context);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext.Severity.ShouldBe(expectedSeverity);
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
        var context = CreateConsumeContext(notification);
        NotificationContext? capturedContext = null;

        var provider = Substitute.For<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationService
            .GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto> { providerDto });

        _providerFactory
            .CreateProvider(Arg.Any<NotificationProviderDto>())
            .Returns(provider);

        provider
            .SendNotificationAsync(Arg.Any<NotificationContext>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedContext = ci.ArgAt<NotificationContext>(0));

        // Act
        await ConsumeWithTimeAdvance(consumer, context);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext.Data["Instance type"].ShouldBe("Sonarr");
        capturedContext.Data["Url"].ShouldBe("http://sonarr.local/");
        capturedContext.Data["Hash"].ShouldBe("ABC123");
        capturedContext.Image.ShouldBe(new Uri("http://example.com/image.jpg"));
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
        var context = CreateConsumeContext(notification);
        NotificationContext? capturedContext = null;

        var provider = Substitute.For<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationService
            .GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto> { providerDto });

        _providerFactory
            .CreateProvider(Arg.Any<NotificationProviderDto>())
            .Returns(provider);

        provider
            .SendNotificationAsync(Arg.Any<NotificationContext>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedContext = ci.ArgAt<NotificationContext>(0));

        // Act
        await ConsumeWithTimeAdvance(consumer, context);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext.Data["CustomKey1"].ShouldBe("CustomValue1");
        capturedContext.Data["CustomKey2"].ShouldBe("CustomValue2");
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
        var context = CreateConsumeContext(notification);
        NotificationContext? capturedContext = null;

        var provider = Substitute.For<INotificationProvider>();
        var providerDto = new NotificationProviderDto { Id = Guid.NewGuid(), Name = "Test Provider", Type = NotificationProviderType.Apprise };

        _configurationService
            .GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto> { providerDto });

        _providerFactory
            .CreateProvider(Arg.Any<NotificationProviderDto>())
            .Returns(provider);

        provider
            .SendNotificationAsync(Arg.Any<NotificationContext>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedContext = ci.ArgAt<NotificationContext>(0));

        // Act
        await ConsumeWithTimeAdvance(consumer, context);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext.Data.ContainsKey("Instance type").ShouldBeFalse();
        capturedContext.Data.ContainsKey("Url").ShouldBeFalse();
        capturedContext.Data.ContainsKey("Hash").ShouldBeFalse();
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
        var context = CreateConsumeContext(notification);

        _configurationService
            .GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto>());

        // Act
        await ConsumeWithTimeAdvance(consumer, context);

        // Assert
        _providerFactory.DidNotReceive().CreateProvider(Arg.Any<NotificationProviderDto>());
    }

    #endregion

    #region Helper Methods

    private NotificationConsumer<T> CreateConsumer<T>() where T : Notification
    {
        var logger = Substitute.For<ILogger<NotificationConsumer<T>>>();
        return new NotificationConsumer<T>(logger, _notificationService, _timeProvider);
    }

    private static ConsumeContext<T> CreateConsumeContext<T>(T message) where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        return context;
    }

    /// <summary>
    /// Executes the consumer and advances time past the 1-second spam prevention delay
    /// </summary>
    private async Task ConsumeWithTimeAdvance<T>(NotificationConsumer<T> consumer, ConsumeContext<T> context) where T : Notification
    {
        var task = consumer.Consume(context);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        await task;
    }

    #endregion
}
