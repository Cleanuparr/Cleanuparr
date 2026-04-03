using System.Text.Json;
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
        strikeEvent.Data.ShouldNotBeNull();
        using (var data = JsonDocument.Parse(strikeEvent.Data!))
        {
            data.RootElement.GetProperty("hash").GetString().ShouldBe("STALLED_HASH_123");
            data.RootElement.GetProperty("itemName").GetString().ShouldBe("Stalled.Movie.2024.1080p");
            data.RootElement.GetProperty("strikeCount").GetInt32().ShouldBe(1);
            data.RootElement.GetProperty("strikeType").GetString().ShouldBe("Stalled");
        }

        // Assert: Notification sent
        await _fixture.NotificationPublisher.Received(1).NotifyStrike(StrikeType.Stalled, 1);
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
        strikeEvent.Data.ShouldNotBeNull();
        using (var data = JsonDocument.Parse(strikeEvent.Data!))
        {
            data.RootElement.GetProperty("hash").GetString().ShouldBe("METADATA_HASH_456");
            data.RootElement.GetProperty("itemName").GetString().ShouldBe("Metadata.Movie.2024.1080p");
            data.RootElement.GetProperty("strikeCount").GetInt32().ShouldBe(1);
            data.RootElement.GetProperty("strikeType").GetString().ShouldBe("DownloadingMetadata");
        }

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
        strikeEvent.Data.ShouldNotBeNull();
        using (var data = JsonDocument.Parse(strikeEvent.Data!))
        {
            data.RootElement.GetProperty("hash").GetString().ShouldBe("FAILED_HASH_789");
            data.RootElement.GetProperty("itemName").GetString().ShouldBe("FailedImport.Movie.2024.1080p");
            data.RootElement.GetProperty("strikeCount").GetInt32().ShouldBe(1);
            data.RootElement.GetProperty("strikeType").GetString().ShouldBe("FailedImport");

            // FailedImport-specific: includes failedImportReasons from QueueRecord.StatusMessages
            var reasons = data.RootElement.GetProperty("failedImportReasons");
            reasons.GetArrayLength().ShouldBe(1);
            reasons[0].GetProperty("Title").GetString().ShouldBe("Import failed");
            var messages = reasons[0].GetProperty("Messages");
            messages.GetArrayLength().ShouldBe(2);
            messages[0].GetString().ShouldBe("File not found");
            messages[1].GetString().ShouldBe("Path does not exist");
        }

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
        strikeEvent.Data.ShouldNotBeNull();
        using (var data = JsonDocument.Parse(strikeEvent.Data!))
        {
            data.RootElement.GetProperty("hash").GetString().ShouldBe("SLOW_SPEED_HASH_111");
            data.RootElement.GetProperty("itemName").GetString().ShouldBe("SlowSpeed.Movie.2024.1080p");
            data.RootElement.GetProperty("strikeCount").GetInt32().ShouldBe(1);
            data.RootElement.GetProperty("strikeType").GetString().ShouldBe("SlowSpeed");
        }

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
        strikeEvent.Data.ShouldNotBeNull();
        using (var data = JsonDocument.Parse(strikeEvent.Data!))
        {
            data.RootElement.GetProperty("hash").GetString().ShouldBe("SLOW_TIME_HASH_222");
            data.RootElement.GetProperty("itemName").GetString().ShouldBe("SlowTime.Movie.2024.1080p");
            data.RootElement.GetProperty("strikeCount").GetInt32().ShouldBe(1);
            data.RootElement.GetProperty("strikeType").GetString().ShouldBe("SlowTime");
        }

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
            using var data = JsonDocument.Parse(events[i].Data!);
            data.RootElement.GetProperty("strikeCount").GetInt32().ShouldBe(i + 1);
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
        manualEvents[0].Data.ShouldNotBeNull();
        using (var data = JsonDocument.Parse(manualEvents[0].Data!))
        {
            data.RootElement.GetProperty("itemName").GetString().ShouldBe("Recurring.Movie.2024");
            data.RootElement.GetProperty("hash").GetString().ShouldBe("RECURRING_HASH_555");
            data.RootElement.GetProperty("strikeCount").GetInt32().ShouldBe(3);
        }
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
        events[0].Data.ShouldNotBeNull();
        using (var data = JsonDocument.Parse(events[0].Data!))
        {
            var reasons = data.RootElement.GetProperty("failedImportReasons");
            reasons.GetArrayLength().ShouldBe(0);
        }
    }
}
