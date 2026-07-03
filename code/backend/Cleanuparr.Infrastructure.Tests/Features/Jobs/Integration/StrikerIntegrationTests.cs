using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs.Integration;

[Collection(IntegrationTestCollection.Name)]
public class StrikerIntegrationTests : IDisposable
{
    private readonly IntegrationTestFixture _fixture;
    private readonly Guid _arrInstanceId = Guid.NewGuid();

    public StrikerIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
        SetupContext();
    }

    public void Dispose()
    {
        Striker.RecurringHashes.Clear();
    }

    private void SetupContext()
    {
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceId, _arrInstanceId);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, new Uri("http://radarr:7878"));
        ContextProvider.Set(nameof(InstanceType), (object)InstanceType.Radarr);
    }

    [Fact]
    public async Task StalledStrike_PublishesEvent_CreatesStrike_SendsNotification()
    {
        // Act
        bool shouldRemove = await _fixture.Striker.StrikeAndCheckLimit(
            "STALLED_HASH_123", "Stalled.Movie.2024.1080p", maxStrikes: 3, StrikeType.Stalled);

        // Assert: Should not remove (1 of 3 strikes)
        shouldRemove.ShouldBe(false);

        // Assert: Strike record
        var strikes = await _fixture.EventsContext.Strikes.ToListAsync();
        strikes.Count.ShouldBe(1);
        strikes[0].Type.ShouldBe(StrikeType.Stalled);
        strikes[0].JobRunId.ShouldBe(_fixture.JobRunId);
        strikes[0].IsDryRun.ShouldBe(false);
        strikes[0].LastDownloadedBytes.ShouldBeNull();

        // Assert: DownloadItem record
        var downloadItems = await _fixture.EventsContext.DownloadItems.ToListAsync();
        downloadItems.Count.ShouldBe(1);
        downloadItems[0].DownloadId.ShouldBe("STALLED_HASH_123");
        downloadItems[0].Title.ShouldBe("Stalled.Movie.2024.1080p");
        downloadItems[0].IsMarkedForRemoval.ShouldBe(false);
        downloadItems[0].IsRemoved.ShouldBe(false);
        downloadItems[0].IsReturning.ShouldBe(false);

        // Assert: FK relationship
        strikes[0].DownloadItemId.ShouldBe(downloadItems[0].Id);

        // Assert: Full AppEvent property verification
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(1);

        var strikeEvent = events[0];
        strikeEvent.EventType.ShouldBe(EventType.StalledStrike);
        strikeEvent.Message.ShouldBe("Item 'Stalled.Movie.2024.1080p' has been struck 1 times for reason 'Stalled'");
        strikeEvent.Severity.ShouldBe(EventSeverity.Important);
        strikeEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        strikeEvent.ArrInstanceId.ShouldBe(_arrInstanceId);
        strikeEvent.DownloadClientId.ShouldBeNull();
        strikeEvent.IsDryRun.ShouldBe(false);
        strikeEvent.StrikeId.ShouldBe(strikes[0].Id);
        strikeEvent.TrackingId.ShouldBeNull();
        strikeEvent.SearchStatus.ShouldBeNull();
        strikeEvent.CompletedAt.ShouldBeNull();
        strikeEvent.CycleId.ShouldBeNull();
        strikeEvent.ItemHash.ShouldBe("STALLED_HASH_123");
        strikeEvent.ItemTitle.ShouldBe("Stalled.Movie.2024.1080p");
        strikeEvent.StrikeCount.ShouldBe(1);

        // Assert: Notification sent
        await _fixture.NotificationPublisher.Received(1).NotifyStrike(StrikeType.Stalled, 1);
    }

    [Fact]
    public async Task ResetStrikeAsync_ClearsActiveStrikes_PublishesStrikeResetEvent()
    {
        // Arrange: two stalled strikes on the same item
        await _fixture.Striker.StrikeAndCheckLimit("RESET_HASH", "Recovered.Movie.2024", maxStrikes: 5, StrikeType.Stalled);
        await _fixture.Striker.StrikeAndCheckLimit("RESET_HASH", "Recovered.Movie.2024", maxStrikes: 5, StrikeType.Stalled);
        (await _fixture.EventsContext.Strikes.CountAsync()).ShouldBe(2);

        // Act
        await _fixture.Striker.ResetStrikeAsync("RESET_HASH", "Recovered.Movie.2024", StrikeType.Stalled);

        // Assert: active strikes of that type are cleared (history lives in the event stream)
        (await _fixture.EventsContext.Strikes.ToListAsync()).ShouldBeEmpty();

        // Assert: exactly one StrikeReset event, with typed payload
        var resetEvents = await _fixture.EventsContext.Events
            .Where(e => e.EventType == EventType.StrikeReset)
            .ToListAsync();
        resetEvents.Count.ShouldBe(1);
        resetEvents[0].Severity.ShouldBe(EventSeverity.Information);
        resetEvents[0].ItemHash.ShouldBe("RESET_HASH");
        resetEvents[0].ItemTitle.ShouldBe("Recovered.Movie.2024");
        resetEvents[0].StrikeCount.ShouldBe(2);
    }

    [Fact]
    public async Task DownloadingMetadataStrike_PublishesEvent_CreatesStrike_SendsNotification()
    {
        // Act
        bool shouldRemove = await _fixture.Striker.StrikeAndCheckLimit(
            "METADATA_HASH_456", "Metadata.Movie.2024.1080p", maxStrikes: 3, StrikeType.DownloadingMetadata);

        // Assert: Should not remove (1 of 3 strikes)
        shouldRemove.ShouldBe(false);

        // Assert: Strike record
        var strikes = await _fixture.EventsContext.Strikes.ToListAsync();
        strikes.Count.ShouldBe(1);
        strikes[0].Type.ShouldBe(StrikeType.DownloadingMetadata);
        strikes[0].JobRunId.ShouldBe(_fixture.JobRunId);
        strikes[0].IsDryRun.ShouldBe(false);
        strikes[0].LastDownloadedBytes.ShouldBeNull();

        // Assert: DownloadItem record
        var downloadItems = await _fixture.EventsContext.DownloadItems.ToListAsync();
        downloadItems.Count.ShouldBe(1);
        downloadItems[0].DownloadId.ShouldBe("METADATA_HASH_456");
        downloadItems[0].Title.ShouldBe("Metadata.Movie.2024.1080p");
        downloadItems[0].IsMarkedForRemoval.ShouldBe(false);

        // Assert: FK relationship
        strikes[0].DownloadItemId.ShouldBe(downloadItems[0].Id);

        // Assert: Full AppEvent property verification
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(1);

        var strikeEvent = events[0];
        strikeEvent.EventType.ShouldBe(EventType.DownloadingMetadataStrike);
        strikeEvent.Message.ShouldBe("Item 'Metadata.Movie.2024.1080p' has been struck 1 times for reason 'DownloadingMetadata'");
        strikeEvent.Severity.ShouldBe(EventSeverity.Important);
        strikeEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        strikeEvent.ArrInstanceId.ShouldBe(_arrInstanceId);
        strikeEvent.DownloadClientId.ShouldBeNull();
        strikeEvent.IsDryRun.ShouldBe(false);
        strikeEvent.StrikeId.ShouldBe(strikes[0].Id);
        strikeEvent.TrackingId.ShouldBeNull();
        strikeEvent.SearchStatus.ShouldBeNull();
        strikeEvent.CompletedAt.ShouldBeNull();
        strikeEvent.CycleId.ShouldBeNull();
        strikeEvent.ItemHash.ShouldBe("METADATA_HASH_456");
        strikeEvent.ItemTitle.ShouldBe("Metadata.Movie.2024.1080p");
        strikeEvent.StrikeCount.ShouldBe(1);

        // Assert: Notification sent
        await _fixture.NotificationPublisher.Received(1).NotifyStrike(StrikeType.DownloadingMetadata, 1);
    }

    [Fact]
    public async Task FailedImportStrike_PublishesEvent_WithStatusMessages_SendsNotification()
    {
        // Arrange: FailedImport reads QueueRecord from ContextProvider for StatusMessages
        var queueRecord = new QueueRecord
        {
            Id = 1,
            Title = "FailedImport.Movie.2024.1080p",
            Protocol = "torrent",
            DownloadId = "FAILED_HASH_789",
            StatusMessages =
            [
                new TrackedDownloadStatusMessage
                {
                    Title = "Import failed",
                    Messages = ["File not found", "Path does not exist"]
                }
            ]
        };
        ContextProvider.Set(nameof(QueueRecord), queueRecord);

        // Act
        bool shouldRemove = await _fixture.Striker.StrikeAndCheckLimit(
            "FAILED_HASH_789", "FailedImport.Movie.2024.1080p", maxStrikes: 3, StrikeType.FailedImport);

        // Assert: Should not remove (1 of 3 strikes)
        shouldRemove.ShouldBe(false);

        // Assert: Strike record
        var strikes = await _fixture.EventsContext.Strikes.ToListAsync();
        strikes.Count.ShouldBe(1);
        strikes[0].Type.ShouldBe(StrikeType.FailedImport);
        strikes[0].JobRunId.ShouldBe(_fixture.JobRunId);
        strikes[0].IsDryRun.ShouldBe(false);
        strikes[0].LastDownloadedBytes.ShouldBeNull();

        // Assert: DownloadItem record
        var downloadItems = await _fixture.EventsContext.DownloadItems.ToListAsync();
        downloadItems.Count.ShouldBe(1);
        downloadItems[0].DownloadId.ShouldBe("FAILED_HASH_789");
        downloadItems[0].Title.ShouldBe("FailedImport.Movie.2024.1080p");
        downloadItems[0].IsMarkedForRemoval.ShouldBe(false);

        // Assert: FK relationship
        strikes[0].DownloadItemId.ShouldBe(downloadItems[0].Id);

        // Assert: Full AppEvent property verification
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(1);

        var strikeEvent = events[0];
        strikeEvent.EventType.ShouldBe(EventType.FailedImportStrike);
        strikeEvent.Message.ShouldBe("Item 'FailedImport.Movie.2024.1080p' has been struck 1 times for reason 'FailedImport'");
        strikeEvent.Severity.ShouldBe(EventSeverity.Important);
        strikeEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        strikeEvent.ArrInstanceId.ShouldBe(_arrInstanceId);
        strikeEvent.DownloadClientId.ShouldBeNull();
        strikeEvent.IsDryRun.ShouldBe(false);
        strikeEvent.StrikeId.ShouldBe(strikes[0].Id);
        strikeEvent.TrackingId.ShouldBeNull();
        strikeEvent.SearchStatus.ShouldBeNull();
        strikeEvent.CompletedAt.ShouldBeNull();
        strikeEvent.CycleId.ShouldBeNull();
        strikeEvent.ItemHash.ShouldBe("FAILED_HASH_789");
        strikeEvent.ItemTitle.ShouldBe("FailedImport.Movie.2024.1080p");
        strikeEvent.StrikeCount.ShouldBe(1);

        // FailedImport-specific: includes failedImportReasons from QueueRecord.StatusMessages
        strikeEvent.FailedImportReasons.Count.ShouldBe(1);
        strikeEvent.FailedImportReasons[0].ShouldContain("Import failed");
        strikeEvent.FailedImportReasons[0].ShouldContain("File not found");
        strikeEvent.FailedImportReasons[0].ShouldContain("Path does not exist");

        // Assert: Notification sent
        await _fixture.NotificationPublisher.Received(1).NotifyStrike(StrikeType.FailedImport, 1);
    }

    [Fact]
    public async Task SlowSpeedStrike_PublishesEvent_CreatesStrike_SendsNotification()
    {
        // Act
        bool shouldRemove = await _fixture.Striker.StrikeAndCheckLimit(
            "SLOW_SPEED_HASH_111", "SlowSpeed.Movie.2024.1080p", maxStrikes: 3, StrikeType.SlowSpeed);

        // Assert: Should not remove (1 of 3 strikes)
        shouldRemove.ShouldBe(false);

        // Assert: Strike record
        var strikes = await _fixture.EventsContext.Strikes.ToListAsync();
        strikes.Count.ShouldBe(1);
        strikes[0].Type.ShouldBe(StrikeType.SlowSpeed);
        strikes[0].JobRunId.ShouldBe(_fixture.JobRunId);
        strikes[0].IsDryRun.ShouldBe(false);
        strikes[0].LastDownloadedBytes.ShouldBeNull();

        // Assert: DownloadItem record
        var downloadItems = await _fixture.EventsContext.DownloadItems.ToListAsync();
        downloadItems.Count.ShouldBe(1);
        downloadItems[0].DownloadId.ShouldBe("SLOW_SPEED_HASH_111");
        downloadItems[0].Title.ShouldBe("SlowSpeed.Movie.2024.1080p");
        downloadItems[0].IsMarkedForRemoval.ShouldBe(false);

        // Assert: FK relationship
        strikes[0].DownloadItemId.ShouldBe(downloadItems[0].Id);

        // Assert: Full AppEvent property verification
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(1);

        var strikeEvent = events[0];
        strikeEvent.EventType.ShouldBe(EventType.SlowSpeedStrike);
        strikeEvent.Message.ShouldBe("Item 'SlowSpeed.Movie.2024.1080p' has been struck 1 times for reason 'SlowSpeed'");
        strikeEvent.Severity.ShouldBe(EventSeverity.Important);
        strikeEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        strikeEvent.ArrInstanceId.ShouldBe(_arrInstanceId);
        strikeEvent.DownloadClientId.ShouldBeNull();
        strikeEvent.IsDryRun.ShouldBe(false);
        strikeEvent.StrikeId.ShouldBe(strikes[0].Id);
        strikeEvent.TrackingId.ShouldBeNull();
        strikeEvent.SearchStatus.ShouldBeNull();
        strikeEvent.CompletedAt.ShouldBeNull();
        strikeEvent.CycleId.ShouldBeNull();
        strikeEvent.ItemHash.ShouldBe("SLOW_SPEED_HASH_111");
        strikeEvent.ItemTitle.ShouldBe("SlowSpeed.Movie.2024.1080p");
        strikeEvent.StrikeCount.ShouldBe(1);

        // Assert: Notification sent
        await _fixture.NotificationPublisher.Received(1).NotifyStrike(StrikeType.SlowSpeed, 1);
    }

    [Fact]
    public async Task SlowTimeStrike_PublishesEvent_CreatesStrike_SendsNotification()
    {
        // Act
        bool shouldRemove = await _fixture.Striker.StrikeAndCheckLimit(
            "SLOW_TIME_HASH_222", "SlowTime.Movie.2024.1080p", maxStrikes: 3, StrikeType.SlowTime);

        // Assert: Should not remove (1 of 3 strikes)
        shouldRemove.ShouldBe(false);

        // Assert: Strike record
        var strikes = await _fixture.EventsContext.Strikes.ToListAsync();
        strikes.Count.ShouldBe(1);
        strikes[0].Type.ShouldBe(StrikeType.SlowTime);
        strikes[0].JobRunId.ShouldBe(_fixture.JobRunId);
        strikes[0].IsDryRun.ShouldBe(false);
        strikes[0].LastDownloadedBytes.ShouldBeNull();

        // Assert: DownloadItem record
        var downloadItems = await _fixture.EventsContext.DownloadItems.ToListAsync();
        downloadItems.Count.ShouldBe(1);
        downloadItems[0].DownloadId.ShouldBe("SLOW_TIME_HASH_222");
        downloadItems[0].Title.ShouldBe("SlowTime.Movie.2024.1080p");
        downloadItems[0].IsMarkedForRemoval.ShouldBe(false);

        // Assert: FK relationship
        strikes[0].DownloadItemId.ShouldBe(downloadItems[0].Id);

        // Assert: Full AppEvent property verification
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(1);

        var strikeEvent = events[0];
        strikeEvent.EventType.ShouldBe(EventType.SlowTimeStrike);
        strikeEvent.Message.ShouldBe("Item 'SlowTime.Movie.2024.1080p' has been struck 1 times for reason 'SlowTime'");
        strikeEvent.Severity.ShouldBe(EventSeverity.Important);
        strikeEvent.JobRunId.ShouldBe(_fixture.JobRunId);
        strikeEvent.ArrInstanceId.ShouldBe(_arrInstanceId);
        strikeEvent.DownloadClientId.ShouldBeNull();
        strikeEvent.IsDryRun.ShouldBe(false);
        strikeEvent.StrikeId.ShouldBe(strikes[0].Id);
        strikeEvent.TrackingId.ShouldBeNull();
        strikeEvent.SearchStatus.ShouldBeNull();
        strikeEvent.CompletedAt.ShouldBeNull();
        strikeEvent.CycleId.ShouldBeNull();
        strikeEvent.ItemHash.ShouldBe("SLOW_TIME_HASH_222");
        strikeEvent.ItemTitle.ShouldBe("SlowTime.Movie.2024.1080p");
        strikeEvent.StrikeCount.ShouldBe(1);

        // Assert: Notification sent
        await _fixture.NotificationPublisher.Received(1).NotifyStrike(StrikeType.SlowTime, 1);
    }

    [Fact]
    public async Task StrikeReachingLimit_MarksDownloadItemForRemoval()
    {
        // Act: 3 strikes with maxStrikes=3
        bool result1 = await _fixture.Striker.StrikeAndCheckLimit(
            "LIMIT_HASH_333", "Limit.Movie.2024", maxStrikes: 3, StrikeType.Stalled);
        bool result2 = await _fixture.Striker.StrikeAndCheckLimit(
            "LIMIT_HASH_333", "Limit.Movie.2024", maxStrikes: 3, StrikeType.Stalled);
        bool result3 = await _fixture.Striker.StrikeAndCheckLimit(
            "LIMIT_HASH_333", "Limit.Movie.2024", maxStrikes: 3, StrikeType.Stalled);

        // Assert: First two return false, third returns true
        result1.ShouldBe(false);
        result2.ShouldBe(false);
        result3.ShouldBe(true);

        // Assert: 3 strikes created for same DownloadItem
        var strikes = await _fixture.EventsContext.Strikes.ToListAsync();
        strikes.Count.ShouldBe(3);
        strikes.ShouldAllBe(s => s.Type == StrikeType.Stalled);

        // Assert: Single DownloadItem marked for removal
        var downloadItems = await _fixture.EventsContext.DownloadItems.ToListAsync();
        downloadItems.Count.ShouldBe(1);
        downloadItems[0].IsMarkedForRemoval.ShouldBe(true);

        // Assert: 3 events with incrementing strike counts
        var events = await _fixture.EventsContext.Events.OrderBy(e => e.Timestamp).ToListAsync();
        events.Count.ShouldBe(3);
        for (int i = 0; i < 3; i++)
        {
            events[i].EventType.ShouldBe(EventType.StalledStrike);
            events[i].StrikeCount.ShouldBe(i + 1);
        }

        // Assert: 3 notifications with incrementing counts
        await _fixture.NotificationPublisher.Received(1).NotifyStrike(StrikeType.Stalled, 1);
        await _fixture.NotificationPublisher.Received(1).NotifyStrike(StrikeType.Stalled, 2);
        await _fixture.NotificationPublisher.Received(1).NotifyStrike(StrikeType.Stalled, 3);
    }

    [Fact]
    public async Task DryRunStrike_PublishesEventWithDryRunFlag()
    {
        // Arrange
        _fixture.DryRunInterceptor.IsDryRunEnabled().Returns(true);

        // Act
        bool shouldRemove = await _fixture.Striker.StrikeAndCheckLimit(
            "DRYRUN_HASH_444", "DryRun.Movie.2024", maxStrikes: 1, StrikeType.Stalled);

        // Assert: Should remove (at limit)
        shouldRemove.ShouldBe(true);

        // Assert: Strike has IsDryRun = true
        var strikes = await _fixture.EventsContext.Strikes.ToListAsync();
        strikes.Count.ShouldBe(1);
        strikes[0].IsDryRun.ShouldBe(true);

        // Assert: AppEvent has IsDryRun = true
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(1);
        events[0].IsDryRun.ShouldBe(true);

        // Assert: DownloadItem marked for removal (striker still marks regardless of dry run)
        var downloadItems = await _fixture.EventsContext.DownloadItems.ToListAsync();
        downloadItems[0].IsMarkedForRemoval.ShouldBe(true);
    }

    [Fact]
    public async Task RecurringItem_ExceedsMaxStrikes_PublishesManualEvent()
    {
        // Act: 3 strikes with maxStrikes=2 (strike 3 exceeds limit)
        await _fixture.Striker.StrikeAndCheckLimit(
            "RECURRING_HASH_555", "Recurring.Movie.2024", maxStrikes: 2, StrikeType.Stalled);
        await _fixture.Striker.StrikeAndCheckLimit(
            "RECURRING_HASH_555", "Recurring.Movie.2024", maxStrikes: 2, StrikeType.Stalled);
        await _fixture.Striker.StrikeAndCheckLimit(
            "RECURRING_HASH_555", "Recurring.Movie.2024", maxStrikes: 2, StrikeType.Stalled);

        // Assert: Hash added to RecurringHashes (lowercased)
        Striker.RecurringHashes.ContainsKey("recurring_hash_555").ShouldBe(true);

        // Assert: 3 strike events
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(3);
        events.ShouldAllBe(e => e.EventType == EventType.StalledStrike);

        // Assert: ManualEvent published for recurring item
        var manualEvents = await _fixture.EventsContext.ManualEvents.ToListAsync();
        manualEvents.Count.ShouldBe(1);
        manualEvents[0].Message.ShouldContain("Download keeps coming back after deletion");
        manualEvents[0].Severity.ShouldBe(EventSeverity.Important);
        manualEvents[0].JobRunId.ShouldBe(_fixture.JobRunId);
        manualEvents[0].ItemTitle.ShouldBe("Recurring.Movie.2024");
        manualEvents[0].ItemHash.ShouldBe("RECURRING_HASH_555");
        manualEvents[0].StrikeCount.ShouldBe(3);
    }

    [Fact]
    public async Task StrikeWithLastDownloadedBytes_StoresOnStrikeRecord()
    {
        // Act
        await _fixture.Striker.StrikeAndCheckLimit(
            "BYTES_HASH_666", "Bytes.Movie.2024", maxStrikes: 3, StrikeType.SlowSpeed, lastDownloadedBytes: 1024000);

        // Assert: LastDownloadedBytes persisted
        var strikes = await _fixture.EventsContext.Strikes.ToListAsync();
        strikes.Count.ShouldBe(1);
        strikes[0].LastDownloadedBytes.ShouldBe(1024000);
    }

    [Fact]
    public async Task FailedImportStrike_EmptyStatusMessages_PublishesEmptyReasons()
    {
        // Arrange: QueueRecord with empty StatusMessages
        var queueRecord = new QueueRecord
        {
            Id = 1,
            Title = "EmptyReasons.Movie.2024",
            Protocol = "torrent",
            DownloadId = "EMPTY_REASONS_HASH_777",
            StatusMessages = []
        };
        ContextProvider.Set(nameof(QueueRecord), queueRecord);

        // Act
        await _fixture.Striker.StrikeAndCheckLimit(
            "EMPTY_REASONS_HASH_777", "EmptyReasons.Movie.2024", maxStrikes: 3, StrikeType.FailedImport);

        // Assert: failedImportReasons is an empty array
        var events = await _fixture.EventsContext.Events.ToListAsync();
        events.Count.ShouldBe(1);
        events[0].EventType.ShouldBe(EventType.FailedImportStrike);
        events[0].FailedImportReasons.ShouldBeEmpty();
    }
}
