using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using System.Threading.Channels;
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
    private readonly Channel<NotificationMessage> _notificationQueue;
    private readonly NotificationPublisher _publisher;

    public NotificationPublisherTests()
    {
        _logger = Substitute.For<ILogger<NotificationPublisher>>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _notificationQueue = Channel.CreateUnbounded<NotificationMessage>();

        _dryRunInterceptor.InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ReturnsForAnyArgs(ci => ci.ArgAt<Func<Task>>(0).Invoke());

        _publisher = new NotificationPublisher(
            _logger,
            _dryRunInterceptor,
            _notificationQueue.Writer);
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
    public async Task NotifyStrike_WithStalledStrike_PublishesNotification()
    {
        // Arrange
        SetupContext();
        var rule = new StallRule { Name = "Test Rule" };
        ContextProvider.Set<QueueRule>(rule);

        // Act
        await _publisher.NotifyStrike(StrikeType.Stalled, 1);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.EventType.ShouldBe(NotificationEventType.StalledStrike);
        m.Context.Data.ShouldContainKey("Strike type");
        m.Context.Data["Strike type"].ShouldBe("Stalled");
    }

    [Fact]
    public async Task NotifyStrike_WithFailedImportStrike_MapsToCorrectEventType()
    {
        // Arrange
        SetupContext();

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 2);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.EventType.ShouldBe(NotificationEventType.FailedImportStrike);
        m.Context.Data["Strike count"].ShouldBe("2");
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

        // Act
        await _publisher.NotifyStrike(strikeType, 1);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.EventType.ShouldBe(expectedEventType);
    }

    [Fact]
    public async Task NotifyStrike_WithDeadTorrent_SendsNoNotification_AndDoesNotThrow()
    {
        // Act & Assert
        await _publisher.NotifyStrike(StrikeType.DeadTorrent, 3);

        _notificationQueue.Reader.Count.ShouldBe(0);
    }

    [Fact]
    public async Task NotifyStrike_WithoutExternalUrl_UsesInternalUrlInNotification()
    {
        // Arrange
        SetupContext();

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.Context.Data["Url"].ShouldBe("http://sonarr.local/");
    }

    #endregion

    #region NotifyQueueItemDeleted Tests

    [Fact]
    public async Task NotifyQueueItemDeleted_PublishesNotificationWithCorrectContext()
    {
        // Arrange
        SetupContext();

        // Act
        await _publisher.NotifyQueueItemDeleted(true, DeleteReason.Stalled);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.EventType.ShouldBe(NotificationEventType.QueueItemDeleted);
        m.Context.Data["Reason"].ShouldBe("Stalled");
        m.Context.Data["Removed from client?"].ShouldBe("True");
        m.Context.Severity.ShouldBe(EventSeverity.Important);
    }

    [Fact]
    public async Task NotifyQueueItemDeleted_WhenRemoveFromClientFalse_ReflectsInContext()
    {
        // Arrange
        SetupContext();

        // Act
        await _publisher.NotifyQueueItemDeleted(false, DeleteReason.AllFilesBlocked);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.Context.Data["Removed from client?"].ShouldBe("False");
        m.Context.Data["Reason"].ShouldBe("AllFilesBlocked");
    }

    #endregion

    #region NotifyDownloadCleaned Tests

    [Fact]
    public async Task NotifyDownloadCleaned_PublishesNotificationWithCorrectContext()
    {
        // Arrange
        SetupDownloadCleanerContext();

        // Act
        await _publisher.NotifyDownloadCleaned(2.5, TimeSpan.FromHours(48), "movies", CleanReason.MaxRatioReached);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.EventType.ShouldBe(NotificationEventType.DownloadCleaned);
        m.Context.Description.ShouldBe("Test Download");
        m.Context.Data["Category"].ShouldBe("movies");
        m.Context.Data["Ratio"].ShouldBe("2.5");
        m.Context.Data["Seeding hours"].ShouldBe("48");
    }

    [Fact]
    public async Task NotifyDownloadCleaned_WithSeedingTime_RoundsToWholeHours()
    {
        // Arrange
        SetupDownloadCleanerContext();
        // Act
        await _publisher.NotifyDownloadCleaned(1.0, TimeSpan.FromHours(24.7), "tv", CleanReason.MaxSeedTimeReached);

        // Assert
        NotificationMessage captured = _notificationQueue.ShouldHaveSingle();
        captured.Context.Data["Seeding hours"].ShouldBe("25");
    }

    [Fact]
    public async Task NotifyDownloadCleaned_WithDownloadClientUrl_IncludesUrlInNotification()
    {
        // Arrange
        SetupDownloadCleanerContext();
        ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, new Uri("https://qbit.external.com"));

        // Act
        await _publisher.NotifyDownloadCleaned(2.5, TimeSpan.FromHours(48), "movies", CleanReason.MaxRatioReached);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.Context.Data.ShouldContainKey("Url");
        m.Context.Data["Url"].ShouldBe("https://qbit.external.com/");
    }

    #endregion

    #region NotifyDownloadStopped Tests

    [Fact]
    public async Task NotifyDownloadStopped_PublishesNotificationWithCorrectContext()
    {
        // Arrange
        SetupDownloadCleanerContext();

        // Act
        await _publisher.NotifyDownloadStopped(2.5, TimeSpan.FromHours(48), "movies", CleanReason.MaxRatioReached);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.EventType.ShouldBe(NotificationEventType.DownloadStopped);
        m.Context.Description.ShouldBe("Test Download is no longer seeding. It stays in the download client and its files stay on disk.");
        m.Context.Data["Category"].ShouldBe("movies");
        m.Context.Data["Ratio"].ShouldBe("2.5");
        m.Context.Data["Seeding hours"].ShouldBe("48");
    }

    [Fact]
    public async Task NotifyDownloadStopped_WithSeedingTime_RoundsToWholeHours()
    {
        // Arrange
        SetupDownloadCleanerContext();
        // Act
        await _publisher.NotifyDownloadStopped(1.0, TimeSpan.FromHours(24.7), "tv", CleanReason.MaxSeedTimeReached);

        // Assert
        NotificationMessage captured = _notificationQueue.ShouldHaveSingle();
        captured.Context.Data["Seeding hours"].ShouldBe("25"); // Rounds to 25
    }

    [Fact]
    public async Task NotifyDownloadStopped_WithDownloadClientUrl_IncludesUrlInNotification()
    {
        // Arrange
        SetupDownloadCleanerContext();
        ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, new Uri("https://qbit.external.com"));

        // Act
        await _publisher.NotifyDownloadStopped(2.5, TimeSpan.FromHours(48), "movies", CleanReason.MaxRatioReached);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.Context.Data.ShouldContainKey("Url");
        m.Context.Data["Url"].ShouldBe("https://qbit.external.com/");
    }

    #endregion

    #region NotifyCategoryChanged Tests

    [Fact]
    public async Task NotifyCategoryChanged_WhenNotTag_IncludesOldAndNewCategory()
    {
        // Arrange
        SetupDownloadCleanerContext();

        // Act
        await _publisher.NotifyCategoryChanged("tv-sonarr", "seeding", false);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.EventType.ShouldBe(NotificationEventType.CategoryChanged);
        m.Context.Title.ShouldBe("Category changed");
        m.Context.Data["Old category"].ShouldBe("tv-sonarr");
        m.Context.Data["New category"].ShouldBe("seeding");
    }

    [Fact]
    public async Task NotifyCategoryChanged_WhenIsTag_IncludesOnlyTag()
    {
        // Arrange
        SetupDownloadCleanerContext();
        // Act
        await _publisher.NotifyCategoryChanged("", "seeded", true);

        // Assert
        NotificationMessage captured = _notificationQueue.ShouldHaveSingle();
        captured.Context.Title.ShouldBe("Tag added");
        captured.Context.Data.ShouldContainKey("Tag");
        captured.Context.Data["Tag"].ShouldBe("seeded");
        captured.Context.Data.ShouldNotContainKey("Old category");
        captured.Context.Data.ShouldNotContainKey("New category");
    }

    [Fact]
    public async Task NotifyCategoryChanged_SetsSeverityToInformation()
    {
        // Arrange
        SetupDownloadCleanerContext();

        // Act
        await _publisher.NotifyCategoryChanged("old", "new", false);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.Context.Severity.ShouldBe(EventSeverity.Information);
    }

    #endregion

    #region SendNotificationAsync Tests (through notify methods)

    [Fact]
    public async Task SendNotificationAsync_UsesDryRunInterceptor()
    {
        // Arrange
        SetupContext();

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert
        await _dryRunInterceptor.Received(1).InterceptAsync(
            Arg.Any<Func<Task>>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task SendNotificationAsync_WhenDryRun_DoesNotPublish()
    {
        // Arrange
        SetupContext();
        _dryRunInterceptor.InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .Returns(Task.CompletedTask);

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert
        _notificationQueue.Reader.Count.ShouldBe(0);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task NotifyStrike_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        // Setup dry run interceptor to throw when called
        _dryRunInterceptor.InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ThrowsAsync(new Exception("Interceptor failed"));

        SetupContext();

        // Act
        await _publisher.NotifyStrike(StrikeType.FailedImport, 1);

        // Assert
        _logger.HasLogContaining(LogLevel.Error, "failed to notify strike").ShouldBeTrue();
    }

    [Fact]
    public async Task NotifyQueueItemDeleted_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        _dryRunInterceptor.InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ThrowsAsync(new Exception("Error"));

        SetupContext();

        // Act
        await _publisher.NotifyQueueItemDeleted(true, DeleteReason.Stalled);

        // Assert
        _logger.HasLogContaining(LogLevel.Error, "Failed to notify queue item deleted").ShouldBeTrue();
    }

    [Fact]
    public async Task NotifyDownloadCleaned_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        _dryRunInterceptor.InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ThrowsAsync(new Exception("Error"));

        SetupDownloadCleanerContext();

        // Act
        await _publisher.NotifyDownloadCleaned(1.0, TimeSpan.FromHours(1), "test", CleanReason.MaxRatioReached);

        // Assert
        _logger.HasLogContaining(LogLevel.Error, "Failed to notify download cleaned").ShouldBeTrue();
    }

    [Fact]
    public async Task NotifyDownloadStopped_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        _dryRunInterceptor.InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ThrowsAsync(new Exception("Error"));

        SetupDownloadCleanerContext();

        // Act
        await _publisher.NotifyDownloadStopped(1.0, TimeSpan.FromHours(1), "test", CleanReason.MaxRatioReached);

        // Assert
        _logger.HasLogContaining(LogLevel.Error, "Failed to notify download stopped").ShouldBeTrue();
    }

    [Fact]
    public async Task NotifyCategoryChanged_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        _dryRunInterceptor.InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ThrowsAsync(new Exception("Error"));

        SetupDownloadCleanerContext();

        // Act
        await _publisher.NotifyCategoryChanged("old", "new", false);

        // Assert
        _logger.HasLogContaining(LogLevel.Error, "Failed to notify category changed").ShouldBeTrue();
    }

    #endregion

    #region NotifySearchItemGrabbed Tests

    [Fact]
    public async Task NotifySearchItemGrabbed_PublishesNotificationWithCorrectContext()
    {
        // Arrange
        var grabbedItems = new List<string> { "Movie.A.2024.1080p", "Movie.A.2024.720p" };

        // Act
        await _publisher.NotifySearchItemGrabbed("Movie A", grabbedItems, InstanceType.Radarr, "http://radarr.local:7878");

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.EventType.ShouldBe(NotificationEventType.SearchItemGrabbed);
        m.Context.Title.ShouldBe("Download grabbed");
        m.Context.Description.ShouldBe("Movie A");
        m.Context.Severity.ShouldBe(EventSeverity.Information);
        m.Context.Data["Item"].ShouldBe("Movie A");
        m.Context.Data["Grabbed"].ShouldBe("Movie.A.2024.1080p, Movie.A.2024.720p");
        m.Context.Data["Instance type"].ShouldBe("Radarr");
        m.Context.Data["Url"].ShouldBe("http://radarr.local:7878");
    }

    [Fact]
    public async Task NotifySearchItemGrabbed_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        _dryRunInterceptor.InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ThrowsAsync(new Exception("Error"));

        // Act
        await _publisher.NotifySearchItemGrabbed("Movie A", ["Movie.A.2024"], InstanceType.Radarr, "http://localhost:7878");

        // Assert
        _logger.HasLogContaining(LogLevel.Error, "Failed to notify search item grabbed").ShouldBeTrue();
    }

    #endregion

    #region NotifyForceImported Tests

    [Fact]
    public async Task NotifyForceImported_PublishesNotificationWithCorrectContext()
    {
        // Arrange
        SetupContext();

        // Act
        await _publisher.NotifyForceImported();

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.EventType.ShouldBe(NotificationEventType.ForceImported);
        m.Context.Title.ShouldBe("Imported a download the arr had blocked");
        m.Context.Description.ShouldBe("Test Show");
        m.Context.Severity.ShouldBe(EventSeverity.Important);
        m.Context.Data["Hash"].ShouldBe("abcd1234");
        m.Context.Data["Instance type"].ShouldBe("Sonarr");
        m.Context.Data["Url"].ShouldBe("http://sonarr.local/");
    }

    [Fact]
    public async Task NotifyForceImported_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        SetupContext();
        _dryRunInterceptor.InterceptAsync(Arg.Any<Func<Task>>(), Arg.Any<string?>())
            .ThrowsAsync(new Exception("Error"));

        // Act
        await _publisher.NotifyForceImported();

        // Assert
        _logger.HasLogContaining(LogLevel.Error, "Failed to notify force imported").ShouldBeTrue();
    }

    #endregion

    #region LazyLibrarian

    // LazyLibrarian carries no QueueRecord, so the publisher reads the item from context.
    private void SetupLazyLibrarianContext()
    {
        ContextProvider.Set(nameof(InstanceType), InstanceType.LazyLibrarian);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, new Uri("http://lazylibrarian.local"));
        ContextProvider.Set(ContextProvider.Keys.Version, 1f);
        ContextProvider.Set(ContextProvider.Keys.ItemName, "Frankenstein");
        ContextProvider.Set(ContextProvider.Keys.Hash, "BOOKHASH1");
    }

    [Fact]
    public async Task NotifyQueueItemDeleted_WithoutAQueueRecord_StillNotifies()
    {
        // Arrange
        SetupLazyLibrarianContext();

        // Act
        await _publisher.NotifyQueueItemDeleted(true, DeleteReason.Stalled);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.Context.Description.ShouldBe("Frankenstein");
        m.Context.Data["Hash"].ShouldBe("bookhash1");
    }

    [Fact]
    public async Task NotifyStrike_WithoutAQueueRecord_StillNotifies()
    {
        // Arrange
        SetupLazyLibrarianContext();
        ContextProvider.Set<QueueRule>(new StallRule { Name = "Test Rule" });

        // Act
        await _publisher.NotifyStrike(StrikeType.Stalled, 1);

        // Assert
        NotificationMessage m = _notificationQueue.ShouldHaveSingle();
        m.Context.Description.ShouldBe("Frankenstein");
        m.Context.Data["Hash"].ShouldBe("bookhash1");
    }

    #endregion
}
