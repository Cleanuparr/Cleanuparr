using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Notifications;

public class NotificationPublisherTests
{
    private readonly ILogger<NotificationPublisher> _logger;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly INotificationConfigurationService _configService;
    private readonly INotificationProviderFactory _providerFactory;
    private readonly NotificationPublisher _publisher;

    public NotificationPublisherTests()
    {
        _logger = Substitute.For<ILogger<NotificationPublisher>>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _configService = Substitute.For<INotificationConfigurationService>();
        _providerFactory = Substitute.For<INotificationProviderFactory>();

        // Setup dry run interceptor to call through
        _dryRunInterceptor.InterceptAsync(default!, default!)
            .ReturnsForAnyArgs(ci =>
            {
                var action = ci.ArgAt<Delegate>(0);
                var parameters = ci.ArgAt<object[]>(1);
                return action.DynamicInvoke(parameters) as Task ?? Task.CompletedTask;
            });

        _publisher = new NotificationPublisher(
            _logger,
            _dryRunInterceptor,
            _configService,
            _providerFactory);
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
        ContextProvider.Set(ContextProvider.Keys.ItemName, "Test Download");
        ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, new Uri("http://downloadclient.local"));
        ContextProvider.Set(ContextProvider.Keys.Hash, "HASH123");
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsAllDependencies()
    {
        // Assert
        _publisher.ShouldNotBeNull();
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
        var provider = Substitute.For<INotificationProvider>();

        _configService.GetProvidersForEventAsync(NotificationEventType.StalledStrike)
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);

        // Act
        await _publisher.NotifyStrike(StrikeType.Stalled, 1);

        // Assert
        await provider.Received(1).SendNotificationAsync(Arg.Is<NotificationContext>(
            c => c.EventType == NotificationEventType.StalledStrike &&
                 c.Data.ContainsKey("Strike type") &&
                 c.Data["Strike type"] == "Stalled"));
    }

    [Fact]
    public async Task NotifyStrike_WithFailedImportStrike_MapsToCorrectEventType()
    {
        // Arrange
        SetupContext();

        var providerDto = CreateProviderDto();
        var provider = Substitute.For<INotificationProvider>();

        _configService.GetProvidersForEventAsync(NotificationEventType.FailedImportStrike)
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 2);

        // Assert
        await provider.Received(1).SendNotificationAsync(Arg.Is<NotificationContext>(
            c => c.EventType == NotificationEventType.FailedImportStrike &&
                 c.Data["Strike count"] == "2"));
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
        var provider = Substitute.For<INotificationProvider>();

        _configService.GetProvidersForEventAsync(expectedEventType)
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);

        // Act
        await _publisher.NotifyStrike(strikeType, 1);

        // Assert
        await _configService.Received(1).GetProvidersForEventAsync(expectedEventType);
    }

    [Fact]
    public async Task NotifyStrike_WhenNoProviders_DoesNotThrow()
    {
        // Arrange
        SetupContext();
        _configService.GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto>());

        // Act & Assert - Should not throw
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);
    }

    [Fact]
    public async Task NotifyStrike_WhenProviderThrows_LogsWarningAndContinues()
    {
        // Arrange
        SetupContext();

        var providerDto = CreateProviderDto();
        var provider = Substitute.For<INotificationProvider>();
        provider.SendNotificationAsync(Arg.Any<NotificationContext>())
            .ThrowsAsync(new Exception("Provider failed"));

        _configService.GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);

        // Act - Should not throw
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Warning, "Failed to send notification");
    }

    [Fact]
    public async Task NotifyStrike_WithoutExternalUrl_UsesInternalUrlInNotification()
    {
        // Arrange
        SetupContext();

        var providerDto = CreateProviderDto();
        var provider = Substitute.For<INotificationProvider>();

        _configService.GetProvidersForEventAsync(NotificationEventType.FailedImportStrike)
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert
        await provider.Received(1).SendNotificationAsync(Arg.Is<NotificationContext>(
            c => c.Data["Url"] == "http://sonarr.local/"));
    }

    #endregion

    #region NotifyQueueItemDeleted Tests

    [Fact]
    public async Task NotifyQueueItemDeleted_SendsNotificationWithCorrectContext()
    {
        // Arrange
        SetupContext();

        var providerDto = CreateProviderDto();
        var provider = Substitute.For<INotificationProvider>();

        _configService.GetProvidersForEventAsync(NotificationEventType.QueueItemDeleted)
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);

        // Act
        await _publisher.NotifyQueueItemDeleted(true, DeleteReason.Stalled);

        // Assert
        await provider.Received(1).SendNotificationAsync(Arg.Is<NotificationContext>(
            c => c.EventType == NotificationEventType.QueueItemDeleted &&
                 c.Data["Reason"] == "Stalled" &&
                 c.Data["Removed from client?"] == "True" &&
                 c.Severity == EventSeverity.Important));
    }

    [Fact]
    public async Task NotifyQueueItemDeleted_WhenRemoveFromClientFalse_ReflectsInContext()
    {
        // Arrange
        SetupContext();

        var providerDto = CreateProviderDto();
        var provider = Substitute.For<INotificationProvider>();

        _configService.GetProvidersForEventAsync(NotificationEventType.QueueItemDeleted)
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);

        // Act
        await _publisher.NotifyQueueItemDeleted(false, DeleteReason.AllFilesBlocked);

        // Assert
        await provider.Received(1).SendNotificationAsync(Arg.Is<NotificationContext>(
            c => c.Data["Removed from client?"] == "False" &&
                 c.Data["Reason"] == "AllFilesBlocked"));
    }

    #endregion

    #region NotifyDownloadCleaned Tests

    [Fact]
    public async Task NotifyDownloadCleaned_SendsNotificationWithCorrectContext()
    {
        // Arrange
        SetupDownloadCleanerContext();

        var providerDto = CreateProviderDto();
        var provider = Substitute.For<INotificationProvider>();

        _configService.GetProvidersForEventAsync(NotificationEventType.DownloadCleaned)
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);

        // Act
        await _publisher.NotifyDownloadCleaned(2.5, TimeSpan.FromHours(48), "movies", CleanReason.MaxRatioReached);

        // Assert
        await provider.Received(1).SendNotificationAsync(Arg.Is<NotificationContext>(
            c => c.EventType == NotificationEventType.DownloadCleaned &&
                 c.Description == "Test Download" &&
                 c.Data["Category"] == "movies" &&
                 c.Data["Ratio"] == "2.5" &&
                 c.Data["Seeding hours"] == "48"));
    }

    [Fact]
    public async Task NotifyDownloadCleaned_WithSeedingTime_RoundsToWholeHours()
    {
        // Arrange
        SetupDownloadCleanerContext();

        var providerDto = CreateProviderDto();
        var provider = Substitute.For<INotificationProvider>();
        NotificationContext? capturedContext = null;

        _configService.GetProvidersForEventAsync(NotificationEventType.DownloadCleaned)
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);
        provider.SendNotificationAsync(Arg.Any<NotificationContext>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedContext = ci.ArgAt<NotificationContext>(0));

        // Act
        await _publisher.NotifyDownloadCleaned(1.0, TimeSpan.FromHours(24.7), "tv", CleanReason.MaxSeedTimeReached);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext.Data["Seeding hours"].ShouldBe("25"); // Rounds to 25
    }

    [Fact]
    public async Task NotifyDownloadCleaned_WithDownloadClientUrl_IncludesUrlInNotification()
    {
        // Arrange
        SetupDownloadCleanerContext();
        ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, new Uri("https://qbit.external.com"));

        var providerDto = CreateProviderDto();
        var provider = Substitute.For<INotificationProvider>();

        _configService.GetProvidersForEventAsync(NotificationEventType.DownloadCleaned)
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);

        // Act
        await _publisher.NotifyDownloadCleaned(2.5, TimeSpan.FromHours(48), "movies", CleanReason.MaxRatioReached);

        // Assert
        await provider.Received(1).SendNotificationAsync(Arg.Is<NotificationContext>(
            c => c.Data.ContainsKey("Url") &&
                 c.Data["Url"] == "https://qbit.external.com/"));
    }

    #endregion

    #region NotifyCategoryChanged Tests

    [Fact]
    public async Task NotifyCategoryChanged_WhenNotTag_IncludesOldAndNewCategory()
    {
        // Arrange
        SetupDownloadCleanerContext();

        var providerDto = CreateProviderDto();
        var provider = Substitute.For<INotificationProvider>();

        _configService.GetProvidersForEventAsync(NotificationEventType.CategoryChanged)
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);

        // Act
        await _publisher.NotifyCategoryChanged("tv-sonarr", "seeding", false);

        // Assert
        await provider.Received(1).SendNotificationAsync(Arg.Is<NotificationContext>(
            c => c.EventType == NotificationEventType.CategoryChanged &&
                 c.Title == "Category changed" &&
                 c.Data["Old category"] == "tv-sonarr" &&
                 c.Data["New category"] == "seeding"));
    }

    [Fact]
    public async Task NotifyCategoryChanged_WhenIsTag_IncludesOnlyTag()
    {
        // Arrange
        SetupDownloadCleanerContext();

        var providerDto = CreateProviderDto();
        var provider = Substitute.For<INotificationProvider>();
        NotificationContext? capturedContext = null;

        _configService.GetProvidersForEventAsync(NotificationEventType.CategoryChanged)
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);
        provider.SendNotificationAsync(Arg.Any<NotificationContext>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedContext = ci.ArgAt<NotificationContext>(0));

        // Act
        await _publisher.NotifyCategoryChanged("", "seeded", true);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext.Title.ShouldBe("Tag added");
        capturedContext.Data.ContainsKey("Tag").ShouldBeTrue();
        capturedContext.Data["Tag"].ShouldBe("seeded");
        capturedContext.Data.ContainsKey("Old category").ShouldBeFalse();
        capturedContext.Data.ContainsKey("New category").ShouldBeFalse();
    }

    [Fact]
    public async Task NotifyCategoryChanged_SetsSeverityToInformation()
    {
        // Arrange
        SetupDownloadCleanerContext();

        var providerDto = CreateProviderDto();
        var provider = Substitute.For<INotificationProvider>();

        _configService.GetProvidersForEventAsync(NotificationEventType.CategoryChanged)
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);

        // Act
        await _publisher.NotifyCategoryChanged("old", "new", false);

        // Assert
        await provider.Received(1).SendNotificationAsync(Arg.Is<NotificationContext>(
            c => c.Severity == EventSeverity.Information));
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
        var provider1 = Substitute.For<INotificationProvider>();
        var provider2 = Substitute.For<INotificationProvider>();

        _configService.GetProvidersForEventAsync(NotificationEventType.FailedImportStrike)
            .Returns(new List<NotificationProviderDto> { providerDto1, providerDto2 });
        _providerFactory.CreateProvider(providerDto1)
            .Returns(provider1);
        _providerFactory.CreateProvider(providerDto2)
            .Returns(provider2);

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert
        await provider1.Received(1).SendNotificationAsync(Arg.Any<NotificationContext>());
        await provider2.Received(1).SendNotificationAsync(Arg.Any<NotificationContext>());
    }

    [Fact]
    public async Task SendNotificationAsync_WhenOneProviderFails_OthersStillSend()
    {
        // Arrange
        SetupContext();

        var providerDto1 = CreateProviderDto("Provider1");
        var providerDto2 = CreateProviderDto("Provider2");
        var provider1 = Substitute.For<INotificationProvider>();
        var provider2 = Substitute.For<INotificationProvider>();

        provider1.SendNotificationAsync(Arg.Any<NotificationContext>())
            .ThrowsAsync(new Exception("Failed"));

        _configService.GetProvidersForEventAsync(NotificationEventType.FailedImportStrike)
            .Returns(new List<NotificationProviderDto> { providerDto1, providerDto2 });
        _providerFactory.CreateProvider(providerDto1)
            .Returns(provider1);
        _providerFactory.CreateProvider(providerDto2)
            .Returns(provider2);

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert - Provider2 should still be called
        await provider2.Received(1).SendNotificationAsync(Arg.Any<NotificationContext>());
    }

    [Fact]
    public async Task SendNotificationAsync_UsesDryRunInterceptor()
    {
        // Arrange
        SetupContext();
        _configService.GetProvidersForEventAsync(Arg.Any<NotificationEventType>())
            .Returns(new List<NotificationProviderDto>());

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert
        await _dryRunInterceptor.Received(1).InterceptAsync(
            Arg.Any<Func<(NotificationEventType, NotificationContext), Task>>(),
            Arg.Any<(NotificationEventType, NotificationContext)>());
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task NotifyStrike_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        // Setup dry run interceptor to throw when called
        _dryRunInterceptor.InterceptAsync(Arg.Any<Delegate>(), Arg.Any<object[]>())
            .ThrowsAsync(new Exception("Interceptor failed"));

        SetupContext();

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "failed to notify strike");
    }

    [Fact]
    public async Task NotifyQueueItemDeleted_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        _dryRunInterceptor.InterceptAsync(Arg.Any<Delegate>(), Arg.Any<object[]>())
            .ThrowsAsync(new Exception("Error"));

        SetupContext();

        // Act
        await _publisher.NotifyQueueItemDeleted(true, DeleteReason.Stalled);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "Failed to notify queue item deleted");
    }

    [Fact]
    public async Task NotifyDownloadCleaned_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        _dryRunInterceptor.InterceptAsync(Arg.Any<Delegate>(), Arg.Any<object[]>())
            .ThrowsAsync(new Exception("Error"));

        SetupDownloadCleanerContext();

        // Act
        await _publisher.NotifyDownloadCleaned(1.0, TimeSpan.FromHours(1), "test", CleanReason.MaxRatioReached);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "Failed to notify download cleaned");
    }

    [Fact]
    public async Task NotifyCategoryChanged_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        _dryRunInterceptor.InterceptAsync(Arg.Any<Delegate>(), Arg.Any<object[]>())
            .ThrowsAsync(new Exception("Error"));

        SetupDownloadCleanerContext();

        // Act
        await _publisher.NotifyCategoryChanged("old", "new", false);

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "Failed to notify category changed");
    }

    #endregion

    #region NotifySearchItemGrabbed Tests

    [Fact]
    public async Task NotifySearchItemGrabbed_SendsNotificationWithCorrectContext()
    {
        // Arrange
        var providerDto = CreateProviderDto();
        var provider = Substitute.For<INotificationProvider>();

        _configService.GetProvidersForEventAsync(NotificationEventType.SearchItemGrabbed)
            .Returns(new List<NotificationProviderDto> { providerDto });
        _providerFactory.CreateProvider(providerDto)
            .Returns(provider);

        var grabbedItems = new List<string> { "Movie.A.2024.1080p", "Movie.A.2024.720p" };

        // Act
        await _publisher.NotifySearchItemGrabbed("Movie A", grabbedItems, InstanceType.Radarr, "http://radarr.local:7878");

        // Assert
        await provider.Received(1).SendNotificationAsync(Arg.Is<NotificationContext>(
            c => c.EventType == NotificationEventType.SearchItemGrabbed &&
                 c.Title == "Download grabbed" &&
                 c.Description == "Movie A" &&
                 c.Severity == EventSeverity.Information &&
                 c.Data["Item"] == "Movie A" &&
                 c.Data["Grabbed"] == "Movie.A.2024.1080p, Movie.A.2024.720p" &&
                 c.Data["Instance type"] == "Radarr" &&
                 c.Data["Url"] == "http://radarr.local:7878"));
    }

    [Fact]
    public async Task NotifySearchItemGrabbed_WhenNoProviders_DoesNotThrow()
    {
        // Arrange
        _configService.GetProvidersForEventAsync(NotificationEventType.SearchItemGrabbed)
            .Returns(new List<NotificationProviderDto>());

        // Act & Assert - Should not throw
        await _publisher.NotifySearchItemGrabbed("Movie A", ["Movie.A.2024"], InstanceType.Radarr, "http://localhost:7878");
    }

    [Fact]
    public async Task NotifySearchItemGrabbed_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        _dryRunInterceptor.InterceptAsync(Arg.Any<Delegate>(), Arg.Any<object[]>())
            .ThrowsAsync(new Exception("Error"));

        // Act
        await _publisher.NotifySearchItemGrabbed("Movie A", ["Movie.A.2024"], InstanceType.Radarr, "http://localhost:7878");

        // Assert
        _logger.ReceivedLogContaining(LogLevel.Error, "Failed to notify search item grabbed");
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
                OnCategoryChanged = true,
                OnSearchTriggered = true,
                OnSearchItemGrabbed = true
            },
            Configuration = new { ApiKey = "test", ChannelId = "123" }
        };
    }

    #endregion
}
