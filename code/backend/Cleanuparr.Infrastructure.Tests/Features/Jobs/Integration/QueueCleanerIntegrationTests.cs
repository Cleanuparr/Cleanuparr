using System.Text.Json;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using QueueCleanerJob = Cleanuparr.Infrastructure.Features.Jobs.QueueCleaner;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs.Integration;

[Collection(IntegrationTestCollection.Name)]
public class QueueCleanerIntegrationTests : IDisposable
{
    private readonly IntegrationTestFixture _fixture;

    public QueueCleanerIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    public void Dispose()
    {
        Striker.RecurringHashes.Clear();
    }

    private QueueCleanerJob CreateSut()
    {
        return new QueueCleanerJob(
            Substitute.For<ILogger<QueueCleanerJob>>(),
            _fixture.DataContext,
            _fixture.Cache,
            _fixture.MessageBus,
            _fixture.ArrClientFactory,
            _fixture.ArrQueueIterator,
            _fixture.DownloadServiceFactory,
            _fixture.EventPublisher);
    }

    [Fact]
    public async Task StalledTorrent_RemovesFromArr_SavesEvent_SendsNotification_AddsToSearchQueue()
    {
        // Arrange
        var instance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        var downloadClient = TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddStallRule(_fixture.DataContext);

        var record = CreateQueueRecord(movieId: 42);

        _fixture.SetupArrQueueIterator(record);
        _fixture.ArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        _fixture.ArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ShouldRemoveFromArrQueueAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(new DownloadCheckResult
            {
                ShouldRemove = true,
                Found = true,
                DeleteReason = DeleteReason.Stalled,
                IsPrivate = false
            });
        _fixture.DownloadServiceFactory.GetDownloadService(Arg.Any<Cleanuparr.Persistence.Models.Configuration.DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert Phase 1: IBus received a remove request
        var removeRequests = _fixture.GetCapturedRemoveRequests();
        removeRequests.Count.ShouldBe(1);

        // Process the captured messages through the real QueueItemRemover pipeline
        _fixture.ArrClient.DeleteQueueItemAsync(
            Arg.Any<ArrInstance>(), Arg.Any<QueueRecord>(), Arg.Any<bool>(), Arg.Any<DeleteReason>())
            .Returns(Task.CompletedTask);

        await _fixture.ProcessCapturedRemoveRequestsAsync();

        // Assert Phase 2: Arr client was told to delete the item
        await _fixture.ArrClient.Received(1).DeleteQueueItemAsync(
            Arg.Is<ArrInstance>(i => i.Id == instance.Id),
            Arg.Is<QueueRecord>(r => r.DownloadId == record.DownloadId),
            true,
            DeleteReason.Stalled);

        // Assert Phase 3: Events persisted with full property verification
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(2);

        // DownloadMarkedForDeletion event
        var markedEvent = events.First(e => e.EventType == EventType.DownloadMarkedForDeletion);
        markedEvent.Message.ShouldBe("Download marked for deletion");
        markedEvent.Severity.ShouldBe(EventSeverity.Important);
        markedEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        markedEvent.ArrInstanceId.ShouldBe(instance.Id);
        markedEvent.DownloadClientId.ShouldBe(mockDownloadService.ClientConfig.Id);
        markedEvent.IsDryRun.ShouldBe(false);
        markedEvent.StrikeId.ShouldBeNull();
        markedEvent.TrackingId.ShouldBeNull();
        markedEvent.SearchStatus.ShouldBeNull();
        markedEvent.CompletedAt.ShouldBeNull();
        markedEvent.CycleId.ShouldBeNull();
        markedEvent.Data.ShouldNotBeNull();
        using (var markedData = JsonDocument.Parse(markedEvent.Data!))
        {
            markedData.RootElement.GetProperty("itemName").GetString().ShouldBe("Test.Movie.2024.1080p");
            markedData.RootElement.GetProperty("hash").GetString().ShouldBe("ABC123DEF456");
        }

        // QueueItemDeleted event
        var deletedEvent = events.First(e => e.EventType == EventType.QueueItemDeleted);
        deletedEvent.Message.ShouldBe("Deleting item from queue with reason: Stalled");
        deletedEvent.Severity.ShouldBe(EventSeverity.Important);
        deletedEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        deletedEvent.ArrInstanceId.ShouldBe(instance.Id);
        deletedEvent.DownloadClientId.ShouldBe(mockDownloadService.ClientConfig.Id);
        deletedEvent.IsDryRun.ShouldBe(false);
        deletedEvent.StrikeId.ShouldBeNull();
        deletedEvent.TrackingId.ShouldBeNull();
        deletedEvent.SearchStatus.ShouldBeNull();
        deletedEvent.CompletedAt.ShouldBeNull();
        deletedEvent.CycleId.ShouldBeNull();
        deletedEvent.Data.ShouldNotBeNull();
        using (var deletedData = JsonDocument.Parse(deletedEvent.Data!))
        {
            deletedData.RootElement.GetProperty("itemName").GetString().ShouldBe("Test.Movie.2024.1080p");
            deletedData.RootElement.GetProperty("hash").GetString().ShouldBe("ABC123DEF456");
            deletedData.RootElement.GetProperty("removeFromClient").GetBoolean().ShouldBe(true);
            deletedData.RootElement.GetProperty("deleteReason").GetString().ShouldBe("Stalled");
        }

        // Assert Phase 4: Notification was triggered
        await _fixture.NotificationPublisher.Received(1).NotifyQueueItemDeleted(true, DeleteReason.Stalled);

        // Assert Phase 5: Replacement search item was added to SearchQueue
        var searchItems = await _fixture.DataContext.SearchQueue.ToListAsync();
        searchItems.Count.ShouldBe(1);
        searchItems[0].ArrInstanceId.ShouldBe(instance.Id);
        searchItems[0].ItemId.ShouldBe(42);
    }

    [Fact]
    public async Task FailedImport_RemovesWithFailedImportReason_SendsNotification()
    {
        // Arrange
        var instance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        var downloadClient = TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var record = CreateQueueRecord(movieId: 99);

        _fixture.SetupArrQueueIterator(record);
        _fixture.ArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        _fixture.ArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);
        _fixture.ArrClient.ShouldRemoveFromQueue(
            Arg.Any<InstanceType>(), Arg.Any<QueueRecord>(), Arg.Any<bool>(), Arg.Any<short>())
            .Returns(true);

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ShouldRemoveFromArrQueueAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(new DownloadCheckResult
            {
                ShouldRemove = false,
                Found = true,
                IsPrivate = false
            });
        _fixture.DownloadServiceFactory.GetDownloadService(Arg.Any<Cleanuparr.Persistence.Models.Configuration.DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert: failed import removal published
        _fixture.GetCapturedRemoveRequests().Count.ShouldBe(1);

        _fixture.ArrClient.DeleteQueueItemAsync(
            Arg.Any<ArrInstance>(), Arg.Any<QueueRecord>(), Arg.Any<bool>(), Arg.Any<DeleteReason>())
            .Returns(Task.CompletedTask);

        await _fixture.ProcessCapturedRemoveRequestsAsync();

        // Full event property verification
        var events = await _fixture.EventsContext.Events.ToListAsync();
        var deletedEvent = events.First(e => e.EventType == EventType.QueueItemDeleted);
        deletedEvent.Message.ShouldBe("Deleting item from queue with reason: FailedImport");
        deletedEvent.Severity.ShouldBe(EventSeverity.Important);
        deletedEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        deletedEvent.ArrInstanceId.ShouldBe(instance.Id);
        deletedEvent.DownloadClientId.ShouldBe(mockDownloadService.ClientConfig.Id);
        deletedEvent.IsDryRun.ShouldBe(false);
        deletedEvent.StrikeId.ShouldBeNull();
        deletedEvent.SearchStatus.ShouldBeNull();
        deletedEvent.Data.ShouldNotBeNull();
        using (var data = JsonDocument.Parse(deletedEvent.Data!))
        {
            data.RootElement.GetProperty("itemName").GetString().ShouldBe("Test.Movie.2024.1080p");
            data.RootElement.GetProperty("hash").GetString().ShouldBe("ABC123DEF456");
            data.RootElement.GetProperty("removeFromClient").GetBoolean().ShouldBe(true);
            data.RootElement.GetProperty("deleteReason").GetString().ShouldBe("FailedImport");
        }

        // Notification with FailedImport reason
        await _fixture.NotificationPublisher.Received(1).NotifyQueueItemDeleted(true, DeleteReason.FailedImport);
    }

    [Fact]
    public async Task IgnoredDownload_IsSkipped_NoEventsOrNotifications()
    {
        // Arrange
        var instance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);

        var record = CreateQueueRecord(downloadId: "IGNORED_HASH_123");

        // Add the download ID to the ignored list
        var generalConfig = await _fixture.DataContext.GeneralConfigs.FirstAsync();
        generalConfig.IgnoredDownloads.Add("IGNORED_HASH_123");
        await _fixture.DataContext.SaveChangesAsync();

        _fixture.SetupArrQueueIterator(record);
        _fixture.ArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        _fixture.ArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert: No removal requests, no events, no notifications
        _fixture.GetCapturedRemoveRequests().ShouldBeEmpty();
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.ShouldBeEmpty();
        await _fixture.NotificationPublisher.DidNotReceive().NotifyQueueItemDeleted(Arg.Any<bool>(), Arg.Any<DeleteReason>());
    }

    [Fact]
    public async Task PrivateTorrent_RemoveFromClientIsFalse()
    {
        // Arrange
        var instance = TestDataContextFactory.AddRadarrInstance(_fixture.DataContext);
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        TestDataContextFactory.AddStallRule(_fixture.DataContext);

        var record = CreateQueueRecord(movieId: 50);

        _fixture.SetupArrQueueIterator(record);
        _fixture.ArrClient.IsRecordValid(Arg.Any<QueueRecord>()).Returns(true);
        _fixture.ArrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);

        var mockDownloadService = _fixture.CreateMockDownloadService();
        mockDownloadService.ShouldRemoveFromArrQueueAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(new DownloadCheckResult
            {
                ShouldRemove = true,
                Found = true,
                DeleteReason = DeleteReason.Stalled,
                IsPrivate = true,
                DeleteFromClient = false
            });
        _fixture.DownloadServiceFactory.GetDownloadService(Arg.Any<Cleanuparr.Persistence.Models.Configuration.DownloadClientConfig>())
            .Returns(mockDownloadService);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert: RemoveFromClient should be false for private torrents
        _fixture.GetCapturedRemoveRequests().Count.ShouldBe(1);

        _fixture.ArrClient.DeleteQueueItemAsync(
            Arg.Any<ArrInstance>(), Arg.Any<QueueRecord>(), Arg.Any<bool>(), Arg.Any<DeleteReason>())
            .Returns(Task.CompletedTask);

        await _fixture.ProcessCapturedRemoveRequestsAsync();

        // The arr client should be told NOT to remove from the download client
        await _fixture.ArrClient.Received(1).DeleteQueueItemAsync(
            Arg.Any<ArrInstance>(),
            Arg.Any<QueueRecord>(),
            false,
            DeleteReason.Stalled);

        // Full event property verification
        var events = await _fixture.EventsContext.Events.ToListAsync();

        var deletedEvent = events.First(e => e.EventType == EventType.QueueItemDeleted);
        deletedEvent.Message.ShouldBe("Deleting item from queue with reason: Stalled");
        deletedEvent.Severity.ShouldBe(EventSeverity.Important);
        deletedEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        deletedEvent.ArrInstanceId.ShouldBe(instance.Id);
        deletedEvent.IsDryRun.ShouldBe(false);
        deletedEvent.Data.ShouldNotBeNull();
        using (var data = JsonDocument.Parse(deletedEvent.Data!))
        {
            data.RootElement.GetProperty("itemName").GetString().ShouldBe("Test.Movie.2024.1080p");
            data.RootElement.GetProperty("hash").GetString().ShouldBe("ABC123DEF456");
            data.RootElement.GetProperty("removeFromClient").GetBoolean().ShouldBe(false);
            data.RootElement.GetProperty("deleteReason").GetString().ShouldBe("Stalled");
        }

        await _fixture.NotificationPublisher.Received(1).NotifyQueueItemDeleted(false, DeleteReason.Stalled);
    }

    private static QueueRecord CreateQueueRecord(
        long movieId = 1,
        string downloadId = "ABC123DEF456",
        string title = "Test.Movie.2024.1080p")
    {
        return new QueueRecord
        {
            Id = 1,
            Title = title,
            Protocol = "torrent",
            DownloadId = downloadId,
            MovieId = movieId,
            Status = "warning",
            StatusMessages = []
        };
    }
}
