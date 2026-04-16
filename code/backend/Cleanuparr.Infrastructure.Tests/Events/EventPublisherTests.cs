using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Events;

public class EventPublisherTests : IDisposable
{
    private readonly EventsContext _context;
    private readonly IHubContext<AppHub> _hubContext;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly IClientProxy _clientProxy;
    private readonly EventPublisher _publisher;

    public EventPublisherTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<EventsContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new EventsContext(options);

        // Setup mocks
        _hubContext = Substitute.For<IHubContext<AppHub>>();
        _notificationPublisher = Substitute.For<INotificationPublisher>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _clientProxy = Substitute.For<IClientProxy>();

        // Setup HubContext to return client proxy
        var clients = Substitute.For<IHubClients>();
        clients.All.Returns(_clientProxy);
        _hubContext.Clients.Returns(clients);

        // Setup dry run interceptor to report dry run as disabled by default
        _dryRunInterceptor.IsDryRunEnabled().Returns(false);

        _publisher = new EventPublisher(
            _context,
            _hubContext,
            Substitute.For<ILogger<EventPublisher>>(),
            _notificationPublisher,
            _dryRunInterceptor);

        // Setup JobRunId in context for tests
        ContextProvider.SetJobRunId(Guid.NewGuid());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region PublishAsync Tests

    [Fact]
    public async Task PublishAsync_SavesEventToDatabase()
    {
        // Arrange
        var eventType = EventType.QueueItemDeleted;
        var message = "Test message";
        var severity = EventSeverity.Important;

        // Act
        await _publisher.PublishAsync(eventType, message, severity);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.EventType.ShouldBe(eventType);
        savedEvent.Message.ShouldBe(message);
        savedEvent.Severity.ShouldBe(severity);
    }

    [Fact]
    public async Task PublishAsync_WithData_SerializesDataToJson()
    {
        // Arrange
        var eventType = EventType.DownloadCleaned;
        var message = "Download cleaned";
        var severity = EventSeverity.Information;
        var data = new { Name = "TestDownload", Hash = "abc123" };

        // Act
        await _publisher.PublishAsync(eventType, message, severity, data);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.Data.ShouldNotBeNull();
        savedEvent.Data.ShouldContain("TestDownload");
        savedEvent.Data.ShouldContain("abc123");
    }

    [Fact]
    public async Task PublishAsync_WithTrackingId_SavesTrackingId()
    {
        // Arrange
        var eventType = EventType.StalledStrike;
        var message = "Strike received";
        var severity = EventSeverity.Warning;
        var trackingId = Guid.NewGuid();

        // Act
        await _publisher.PublishAsync(eventType, message, severity, trackingId: trackingId);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.TrackingId.ShouldBe(trackingId);
    }

    [Fact]
    public async Task PublishAsync_NotifiesSignalRClients()
    {
        // Arrange
        var eventType = EventType.CategoryChanged;
        var message = "Category changed";
        var severity = EventSeverity.Information;

        // Act
        await _publisher.PublishAsync(eventType, message, severity);

        // Assert
        await _clientProxy.Received(1).SendCoreAsync(
            "EventReceived",
            Arg.Is<object[]>(args => args.Length == 1 && args[0] is AppEvent),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_WhenSignalRFails_LogsError()
    {
        // Arrange
        var eventType = EventType.QueueItemDeleted;
        var message = "Test message";
        var severity = EventSeverity.Important;

        _clientProxy.SendCoreAsync(
                Arg.Any<string>(),
                Arg.Any<object[]>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("SignalR connection failed"));

        // Act - should not throw
        await _publisher.PublishAsync(eventType, message, severity);

        // Assert - verify event was still saved
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
    }

    [Fact]
    public async Task PublishAsync_NullData_DoesNotSerialize()
    {
        // Arrange
        var eventType = EventType.DownloadCleaned;
        var message = "Test";
        var severity = EventSeverity.Information;

        // Act
        await _publisher.PublishAsync(eventType, message, severity, data: null);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.Data.ShouldBeNull();
    }

    #endregion

    #region PublishManualAsync Tests

    [Fact]
    public async Task PublishManualAsync_SavesManualEventToDatabase()
    {
        // Arrange
        var message = "Manual event message";
        var severity = EventSeverity.Warning;

        // Act
        await _publisher.PublishManualAsync(message, severity);

        // Assert
        var savedEvent = await _context.ManualEvents.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.Message.ShouldBe(message);
        savedEvent.Severity.ShouldBe(severity);
    }

    [Fact]
    public async Task PublishManualAsync_WithData_SerializesDataToJson()
    {
        // Arrange
        var message = "Manual event";
        var severity = EventSeverity.Important;
        var data = new { ItemName = "TestItem", Count = 5 };

        // Act
        await _publisher.PublishManualAsync(message, severity, data);

        // Assert
        var savedEvent = await _context.ManualEvents.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.Data.ShouldNotBeNull();
        savedEvent.Data.ShouldContain("TestItem");
        savedEvent.Data.ShouldContain("5");
    }

    [Fact]
    public async Task PublishManualAsync_NotifiesSignalRClients()
    {
        // Arrange
        var message = "Manual event";
        var severity = EventSeverity.Information;

        // Act
        await _publisher.PublishManualAsync(message, severity);

        // Assert
        await _clientProxy.Received(1).SendCoreAsync(
            "ManualEventReceived",
            Arg.Is<object[]>(args => args.Length == 1 && args[0] is ManualEvent),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region DryRun Tests

    [Fact]
    public async Task PublishAsync_ChecksDryRunStatus()
    {
        // Arrange
        var eventType = EventType.StalledStrike;
        var message = "Test";
        var severity = EventSeverity.Warning;

        // Act
        await _publisher.PublishAsync(eventType, message, severity);

        // Assert
        await _dryRunInterceptor.Received(1).IsDryRunEnabled();
    }

    [Fact]
    public async Task PublishManualAsync_ChecksDryRunStatus()
    {
        // Arrange
        var message = "Manual test";
        var severity = EventSeverity.Important;

        // Act
        await _publisher.PublishManualAsync(message, severity);

        // Assert
        await _dryRunInterceptor.Received(1).IsDryRunEnabled();
    }

    [Fact]
    public async Task PublishAsync_WhenDryRunEnabled_SetsIsDryRunTrue()
    {
        // Arrange
        _dryRunInterceptor.IsDryRunEnabled().Returns(true);
        var eventType = EventType.StalledStrike;
        var message = "Dry run event";
        var severity = EventSeverity.Warning;

        // Act
        await _publisher.PublishAsync(eventType, message, severity);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.IsDryRun.ShouldBeTrue();
    }

    [Fact]
    public async Task PublishAsync_WhenDryRunDisabled_SetsIsDryRunFalse()
    {
        // Arrange
        var eventType = EventType.QueueItemDeleted;
        var message = "Normal event";
        var severity = EventSeverity.Important;

        // Act
        await _publisher.PublishAsync(eventType, message, severity);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.IsDryRun.ShouldBeFalse();
    }

    [Fact]
    public async Task PublishManualAsync_WhenDryRunEnabled_SetsIsDryRunTrue()
    {
        // Arrange
        _dryRunInterceptor.IsDryRunEnabled().Returns(true);
        var message = "Dry run manual event";
        var severity = EventSeverity.Important;

        // Act
        await _publisher.PublishManualAsync(message, severity);

        // Assert
        var savedEvent = await _context.ManualEvents.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.IsDryRun.ShouldBeTrue();
    }

    [Fact]
    public async Task PublishAsync_WhenDryRunEnabled_StillSavesToDatabase()
    {
        // Arrange
        _dryRunInterceptor.IsDryRunEnabled().Returns(true);
        var eventType = EventType.StalledStrike;
        var message = "Should be saved";
        var severity = EventSeverity.Warning;

        // Act
        await _publisher.PublishAsync(eventType, message, severity);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.Message.ShouldBe(message);
    }

    #endregion

    #region Data Serialization Tests

    [Fact]
    public async Task PublishAsync_SerializesEnumsAsStrings()
    {
        // Arrange
        var eventType = EventType.QueueItemDeleted;
        var message = "Test";
        var severity = EventSeverity.Important;
        var data = new { Reason = DeleteReason.Stalled };

        // Act
        await _publisher.PublishAsync(eventType, message, severity, data);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.Data.ShouldNotBeNull();
        savedEvent.Data.ShouldContain("Stalled");
    }

    [Fact]
    public async Task PublishAsync_HandlesComplexData()
    {
        // Arrange
        var eventType = EventType.DownloadCleaned;
        var message = "Test";
        var severity = EventSeverity.Information;
        var data = new
        {
            Items = new[] { "item1", "item2" },
            Nested = new { Value = 123 },
            NullableValue = (string?)null
        };

        // Act
        await _publisher.PublishAsync(eventType, message, severity, data);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.Data.ShouldNotBeNull();
        savedEvent.Data.ShouldContain("item1");
        savedEvent.Data.ShouldContain("123");
    }

    #endregion

    #region PublishQueueItemDeleted Tests

    [Fact]
    public async Task PublishQueueItemDeleted_SavesEventWithContextData()
    {
        // Arrange
        ContextProvider.Set(ContextProvider.Keys.ItemName, "Test Download");
        ContextProvider.Set(ContextProvider.Keys.Hash, "abc123");

        // Act
        await _publisher.PublishQueueItemDeleted(removeFromClient: true, DeleteReason.Stalled);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.EventType.ShouldBe(EventType.QueueItemDeleted);
        savedEvent.Severity.ShouldBe(EventSeverity.Important);
        savedEvent.Data.ShouldNotBeNull();
        savedEvent.Data.ShouldContain("Test Download");
        savedEvent.Data.ShouldContain("abc123");
        savedEvent.Data.ShouldContain("Stalled");
    }

    [Fact]
    public async Task PublishQueueItemDeleted_SendsNotification()
    {
        // Arrange
        ContextProvider.Set(ContextProvider.Keys.ItemName, "Test Download");
        ContextProvider.Set(ContextProvider.Keys.Hash, "abc123");

        // Act
        await _publisher.PublishQueueItemDeleted(removeFromClient: false, DeleteReason.FailedImport);

        // Assert
        await _notificationPublisher.Received(1).NotifyQueueItemDeleted(false, DeleteReason.FailedImport);
    }

    #endregion

    #region PublishDownloadCleaned Tests

    [Fact]
    public async Task PublishDownloadCleaned_SavesEventWithContextData()
    {
        // Arrange
        ContextProvider.Set(ContextProvider.Keys.ItemName, "Cleaned Download");
        ContextProvider.Set(ContextProvider.Keys.Hash, "def456");

        // Act
        await _publisher.PublishDownloadCleaned(
            ratio: 2.5,
            seedingTime: TimeSpan.FromHours(48),
            categoryName: "movies",
            reason: CleanReason.MaxSeedTimeReached);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.EventType.ShouldBe(EventType.DownloadCleaned);
        savedEvent.Severity.ShouldBe(EventSeverity.Important);
        savedEvent.Data.ShouldNotBeNull();
        savedEvent.Data.ShouldContain("Cleaned Download");
        savedEvent.Data.ShouldContain("def456");
        savedEvent.Data.ShouldContain("movies");
        savedEvent.Data.ShouldContain("MaxSeedTimeReached");
    }

    [Fact]
    public async Task PublishDownloadCleaned_SendsNotification()
    {
        // Arrange
        ContextProvider.Set(ContextProvider.Keys.ItemName, "Test");
        ContextProvider.Set(ContextProvider.Keys.Hash, "xyz");

        var ratio = 1.5;
        var seedingTime = TimeSpan.FromHours(24);
        var categoryName = "tv";
        var reason = CleanReason.MaxRatioReached;

        // Act
        await _publisher.PublishDownloadCleaned(ratio, seedingTime, categoryName, reason);

        // Assert
        await _notificationPublisher.Received(1).NotifyDownloadCleaned(ratio, seedingTime, categoryName, reason);
    }

    #endregion

    #region PublishSearchNotTriggered Tests

    [Fact]
    public async Task PublishSearchNotTriggered_SavesManualEvent()
    {
        // Arrange
        ContextProvider.Set(nameof(InstanceType), (object)InstanceType.Sonarr);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, new Uri("http://localhost:8989"));

        // Act
        await _publisher.PublishSearchNotTriggered("abc123", "Test Item");

        // Assert
        var savedEvent = await _context.ManualEvents.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.Severity.ShouldBe(EventSeverity.Warning);
        savedEvent.Message.ShouldContain("Replacement search was not triggered");
        savedEvent.Data.ShouldNotBeNull();
        savedEvent.Data.ShouldContain("Test Item");
        savedEvent.Data.ShouldContain("abc123");
    }

    #endregion

    #region PublishRecurringItem Tests

    [Fact]
    public async Task PublishRecurringItem_SavesManualEvent()
    {
        // Arrange
        ContextProvider.Set(nameof(InstanceType), (object)InstanceType.Radarr);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, new Uri("http://localhost:7878"));

        // Act
        await _publisher.PublishRecurringItem("hash123", "Recurring Item", 5);

        // Assert
        var savedEvent = await _context.ManualEvents.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.Severity.ShouldBe(EventSeverity.Important);
        savedEvent.Message.ShouldContain("keeps coming back");
        savedEvent.Data.ShouldNotBeNull();
        savedEvent.Data.ShouldContain("Recurring Item");
        savedEvent.Data.ShouldContain("hash123");
    }

    #endregion

    #region PublishCategoryChanged Tests

    [Fact]
    public async Task PublishCategoryChanged_SavesEventWithContextData()
    {
        // Arrange
        ContextProvider.Set(ContextProvider.Keys.ItemName, "Category Test");
        ContextProvider.Set(ContextProvider.Keys.Hash, "cat123");

        // Act
        await _publisher.PublishCategoryChanged("oldCat", "newCat", isTag: false);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.EventType.ShouldBe(EventType.CategoryChanged);
        savedEvent.Severity.ShouldBe(EventSeverity.Information);
        savedEvent.Message.ShouldContain("Category changed from 'oldCat' to 'newCat'");
    }

    [Fact]
    public async Task PublishCategoryChanged_WithTag_SavesCorrectMessage()
    {
        // Arrange
        ContextProvider.Set(ContextProvider.Keys.ItemName, "Tag Test");
        ContextProvider.Set(ContextProvider.Keys.Hash, "tag123");

        // Act
        await _publisher.PublishCategoryChanged("", "cleanuperr-done", isTag: true);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.Message.ShouldContain("Tag 'cleanuperr-done' added");
    }

    [Fact]
    public async Task PublishCategoryChanged_SendsNotification()
    {
        // Arrange
        ContextProvider.Set(ContextProvider.Keys.ItemName, "Test");
        ContextProvider.Set(ContextProvider.Keys.Hash, "xyz");

        // Act
        await _publisher.PublishCategoryChanged("old", "new", isTag: true);

        // Assert
        await _notificationPublisher.Received(1).NotifyCategoryChanged("old", "new", true);
    }

    #endregion

    #region PublishSearchTriggered Tests

    [Fact]
    public async Task PublishSearchTriggered_SavesEventWithCorrectType()
    {
        // Act
        await _publisher.PublishSearchTriggered("Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.EventType.ShouldBe(EventType.SearchTriggered);
        savedEvent.Severity.ShouldBe(EventSeverity.Information);
    }

    [Fact]
    public async Task PublishSearchTriggered_SetsSearchStatusToPending()
    {
        // Act
        await _publisher.PublishSearchTriggered("Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.SearchStatus.ShouldBe(SearchCommandStatus.Pending);
    }

    [Fact]
    public async Task PublishSearchTriggered_SetsCycleId()
    {
        // Arrange
        var cycleId = Guid.NewGuid();

        // Act
        await _publisher.PublishSearchTriggered("Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing, cycleId);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.CycleId.ShouldBe(cycleId);
    }

    [Fact]
    public async Task PublishSearchTriggered_ReturnsEventId()
    {
        // Act
        Guid eventId = await _publisher.PublishSearchTriggered("Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing);

        // Assert
        eventId.ShouldNotBe(Guid.Empty);
        var savedEvent = await _context.Events.FindAsync(eventId);
        savedEvent.ShouldNotBeNull();
    }

    [Fact]
    public async Task PublishSearchTriggered_CreatesSearchEventData()
    {
        // Act
        await _publisher.PublishSearchTriggered("Series A", SeekerSearchType.Replacement, SeekerSearchReason.Replacement);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();

        var searchData = await _context.SearchEventData.FirstOrDefaultAsync(s => s.AppEventId == savedEvent.Id);
        searchData.ShouldNotBeNull();
        searchData.ItemTitle.ShouldBe("Series A");
        searchData.SearchType.ShouldBe(SeekerSearchType.Replacement);
        searchData.SearchReason.ShouldBe(SeekerSearchReason.Replacement);
    }

    [Fact]
    public async Task PublishSearchTriggered_NotifiesSignalRClients()
    {
        // Act
        await _publisher.PublishSearchTriggered("Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing);

        // Assert
        await _clientProxy.Received(1).SendCoreAsync(
            "EventReceived",
            Arg.Is<object[]>(args => args.Length == 1 && args[0] is AppEvent),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishSearchTriggered_SendsNotification()
    {
        // Act
        await _publisher.PublishSearchTriggered("Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing);

        // Assert
        await _notificationPublisher.Received(1).NotifySearchTriggered(
            "Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing);
    }

    [Fact]
    public async Task PublishSearchTriggered_IncludesItemTitleInMessage()
    {
        // Act
        await _publisher.PublishSearchTriggered("The Matrix", SeekerSearchType.Proactive, SeekerSearchReason.Missing);

        // Assert
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        savedEvent.ShouldNotBeNull();
        savedEvent.Message.ShouldContain("The Matrix");
    }

    #endregion

    #region PublishSearchCompleted Tests

    [Fact]
    public async Task PublishSearchCompleted_UpdatesEventStatus()
    {
        // Arrange — create a search event first
        Guid eventId = await _publisher.PublishSearchTriggered("Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing);

        // Act
        await _publisher.PublishSearchCompleted(eventId, SearchCommandStatus.Completed, InstanceType.Radarr, "http://localhost:7878");

        // Assert
        var updatedEvent = await _context.Events.FindAsync(eventId);
        updatedEvent.ShouldNotBeNull();
        updatedEvent.SearchStatus.ShouldBe(SearchCommandStatus.Completed);
    }

    [Fact]
    public async Task PublishSearchCompleted_SetsCompletedAt()
    {
        // Arrange
        Guid eventId = await _publisher.PublishSearchTriggered("Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing);

        // Act
        await _publisher.PublishSearchCompleted(eventId, SearchCommandStatus.Completed, InstanceType.Radarr, "http://localhost:7878");

        // Assert
        var updatedEvent = await _context.Events.FindAsync(eventId);
        updatedEvent.ShouldNotBeNull();
        updatedEvent.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task PublishSearchCompleted_UpdatesGrabbedItemsOnSearchEventData()
    {
        // Arrange
        Guid eventId = await _publisher.PublishSearchTriggered("Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing);

        var grabbedItems = new List<string> { "Movie A (2024)" };

        // Act
        await _publisher.PublishSearchCompleted(eventId, SearchCommandStatus.Completed, InstanceType.Radarr, "http://localhost:7878", grabbedItems);

        // Assert
        var searchData = await _context.SearchEventData.FirstOrDefaultAsync(s => s.AppEventId == eventId);
        searchData.ShouldNotBeNull();
        searchData.GrabbedItems.ShouldContain("Movie A (2024)");
    }

    [Fact]
    public async Task PublishSearchCompleted_WithNullGrabbedItems_DoesNotModifySearchEventData()
    {
        // Arrange
        Guid eventId = await _publisher.PublishSearchTriggered("Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing);

        // Act
        await _publisher.PublishSearchCompleted(eventId, SearchCommandStatus.Completed, InstanceType.Radarr, "http://localhost:7878");

        // Assert
        var searchData = await _context.SearchEventData.FirstOrDefaultAsync(s => s.AppEventId == eventId);
        searchData.ShouldNotBeNull();
        searchData.GrabbedItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task PublishSearchCompleted_EventNotFound_LogsWarningAndReturns()
    {
        // Act — use a non-existent event ID
        await _publisher.PublishSearchCompleted(Guid.NewGuid(), SearchCommandStatus.Completed, InstanceType.Radarr, "http://localhost:7878");

        // Assert — should not throw, and the log warning is the important behavior
        // (no exception thrown is the assertion)
        var eventCount = await _context.Events.CountAsync();
        eventCount.ShouldBe(0);
    }

    [Fact]
    public async Task PublishSearchCompleted_NotifiesSignalRClients()
    {
        // Arrange
        Guid eventId = await _publisher.PublishSearchTriggered("Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing);

        // Reset mock to only capture the completion call
        _clientProxy.ClearReceivedCalls();

        // Act
        await _publisher.PublishSearchCompleted(eventId, SearchCommandStatus.Completed, InstanceType.Radarr, "http://localhost:7878");

        // Assert
        await _clientProxy.Received(1).SendCoreAsync(
            "EventReceived",
            Arg.Is<object[]>(args => args.Length == 1 && args[0] is AppEvent),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishSearchCompleted_WithGrabbedItems_SendsSearchItemGrabbedNotification()
    {
        // Arrange
        Guid eventId = await _publisher.PublishSearchTriggered("Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing);
        var grabbedItems = new List<string> { "Movie A (2024)" };

        // Act
        await _publisher.PublishSearchCompleted(eventId, SearchCommandStatus.Completed, InstanceType.Radarr, "http://localhost:7878", grabbedItems);

        // Assert
        await _notificationPublisher.Received(1).NotifySearchItemGrabbed(
            "Movie A", grabbedItems, InstanceType.Radarr, "http://localhost:7878");
    }

    [Fact]
    public async Task PublishSearchCompleted_WithoutGrabbedItems_DoesNotSendSearchItemGrabbedNotification()
    {
        // Arrange
        Guid eventId = await _publisher.PublishSearchTriggered("Movie A", SeekerSearchType.Proactive, SeekerSearchReason.Missing);

        // Act
        await _publisher.PublishSearchCompleted(eventId, SearchCommandStatus.Completed, InstanceType.Radarr, "http://localhost:7878");

        // Assert
        await _notificationPublisher.DidNotReceive().NotifySearchItemGrabbed(
            Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<InstanceType>(), Arg.Any<string>());
    }

    #endregion
}
