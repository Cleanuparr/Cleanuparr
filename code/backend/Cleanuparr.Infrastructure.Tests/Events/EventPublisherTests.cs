using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Events;

public class EventPublisherTests : IDisposable
{
    private readonly EventsContext _context;
    private readonly Mock<IHubContext<AppHub>> _hubContextMock;
    private readonly Mock<ILogger<EventPublisher>> _loggerMock;
    private readonly Mock<INotificationPublisher> _notificationPublisherMock;
    private readonly Mock<IDryRunInterceptor> _dryRunInterceptorMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly EventPublisher _publisher;

    public EventPublisherTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<EventsContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new EventsContext(options);

        // Setup mocks
        _hubContextMock = new Mock<IHubContext<AppHub>>();
        _loggerMock = new Mock<ILogger<EventPublisher>>();
        _notificationPublisherMock = new Mock<INotificationPublisher>();
        _dryRunInterceptorMock = new Mock<IDryRunInterceptor>();
        _clientProxyMock = new Mock<IClientProxy>();

        // Setup HubContext to return client proxy
        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(c => c.All).Returns(_clientProxyMock.Object);
        _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        // Setup dry run interceptor to execute the delegate
        _dryRunInterceptorMock.Setup(d => d.InterceptAsync(It.IsAny<Delegate>(), It.IsAny<object[]>()))
            .Returns<Delegate, object[]>(async (del, args) =>
            {
                if (del is Func<AppEvent, Task> func && args.Length > 0 && args[0] is AppEvent appEvent)
                {
                    await func(appEvent);
                }
                else if (del is Func<ManualEvent, Task> manualFunc && args.Length > 0 && args[0] is ManualEvent manualEvent)
                {
                    await manualFunc(manualEvent);
                }
            });

        _publisher = new EventPublisher(
            _context,
            _hubContextMock.Object,
            _loggerMock.Object,
            _notificationPublisherMock.Object,
            _dryRunInterceptorMock.Object);

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
        Assert.NotNull(savedEvent);
        Assert.Equal(eventType, savedEvent.EventType);
        Assert.Equal(message, savedEvent.Message);
        Assert.Equal(severity, savedEvent.Severity);
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
        Assert.NotNull(savedEvent);
        Assert.NotNull(savedEvent.Data);
        Assert.Contains("TestDownload", savedEvent.Data);
        Assert.Contains("abc123", savedEvent.Data);
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
        Assert.NotNull(savedEvent);
        Assert.Equal(trackingId, savedEvent.TrackingId);
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
        _clientProxyMock.Verify(c => c.SendCoreAsync(
            "EventReceived",
            It.Is<object[]>(args => args.Length == 1 && args[0] is AppEvent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenSignalRFails_LogsError()
    {
        // Arrange
        var eventType = EventType.QueueItemDeleted;
        var message = "Test message";
        var severity = EventSeverity.Important;

        _clientProxyMock.Setup(c => c.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SignalR connection failed"));

        // Act - should not throw
        await _publisher.PublishAsync(eventType, message, severity);

        // Assert - verify event was still saved
        var savedEvent = await _context.Events.FirstOrDefaultAsync();
        Assert.NotNull(savedEvent);
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
        Assert.NotNull(savedEvent);
        Assert.Null(savedEvent.Data);
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
        Assert.NotNull(savedEvent);
        Assert.Equal(message, savedEvent.Message);
        Assert.Equal(severity, savedEvent.Severity);
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
        Assert.NotNull(savedEvent);
        Assert.NotNull(savedEvent.Data);
        Assert.Contains("TestItem", savedEvent.Data);
        Assert.Contains("5", savedEvent.Data);
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
        _clientProxyMock.Verify(c => c.SendCoreAsync(
            "ManualEventReceived",
            It.Is<object[]>(args => args.Length == 1 && args[0] is ManualEvent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DryRun Interceptor Tests

    [Fact]
    public async Task PublishAsync_UsesDryRunInterceptor()
    {
        // Arrange
        var eventType = EventType.StalledStrike;
        var message = "Test";
        var severity = EventSeverity.Warning;

        // Act
        await _publisher.PublishAsync(eventType, message, severity);

        // Assert
        _dryRunInterceptorMock.Verify(d => d.InterceptAsync(
            It.IsAny<Delegate>(),
            It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task PublishManualAsync_UsesDryRunInterceptor()
    {
        // Arrange
        var message = "Manual test";
        var severity = EventSeverity.Important;

        // Act
        await _publisher.PublishManualAsync(message, severity);

        // Assert
        _dryRunInterceptorMock.Verify(d => d.InterceptAsync(
            It.IsAny<Delegate>(),
            It.IsAny<object[]>()), Times.Once);
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
        Assert.NotNull(savedEvent);
        Assert.NotNull(savedEvent.Data);
        Assert.Contains("Stalled", savedEvent.Data);
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
        Assert.NotNull(savedEvent);
        Assert.NotNull(savedEvent.Data);
        Assert.Contains("item1", savedEvent.Data);
        Assert.Contains("123", savedEvent.Data);
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
        Assert.NotNull(savedEvent);
        Assert.Equal(EventType.QueueItemDeleted, savedEvent.EventType);
        Assert.Equal(EventSeverity.Important, savedEvent.Severity);
        Assert.NotNull(savedEvent.Data);
        Assert.Contains("Test Download", savedEvent.Data);
        Assert.Contains("abc123", savedEvent.Data);
        Assert.Contains("Stalled", savedEvent.Data);
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
        _notificationPublisherMock.Verify(n => n.NotifyQueueItemDeleted(false, DeleteReason.FailedImport), Times.Once);
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
        Assert.NotNull(savedEvent);
        Assert.Equal(EventType.DownloadCleaned, savedEvent.EventType);
        Assert.Equal(EventSeverity.Important, savedEvent.Severity);
        Assert.NotNull(savedEvent.Data);
        Assert.Contains("Cleaned Download", savedEvent.Data);
        Assert.Contains("def456", savedEvent.Data);
        Assert.Contains("movies", savedEvent.Data);
        Assert.Contains("MaxSeedTimeReached", savedEvent.Data);
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
        _notificationPublisherMock.Verify(n => n.NotifyDownloadCleaned(ratio, seedingTime, categoryName, reason), Times.Once);
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
        Assert.NotNull(savedEvent);
        Assert.Equal(EventSeverity.Warning, savedEvent.Severity);
        Assert.Contains("Replacement search was not triggered", savedEvent.Message);
        Assert.NotNull(savedEvent.Data);
        Assert.Contains("Test Item", savedEvent.Data);
        Assert.Contains("abc123", savedEvent.Data);
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
        Assert.NotNull(savedEvent);
        Assert.Equal(EventSeverity.Important, savedEvent.Severity);
        Assert.Contains("keeps coming back", savedEvent.Message);
        Assert.NotNull(savedEvent.Data);
        Assert.Contains("Recurring Item", savedEvent.Data);
        Assert.Contains("hash123", savedEvent.Data);
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
        Assert.NotNull(savedEvent);
        Assert.Equal(EventType.CategoryChanged, savedEvent.EventType);
        Assert.Equal(EventSeverity.Information, savedEvent.Severity);
        Assert.Contains("Category changed from 'oldCat' to 'newCat'", savedEvent.Message);
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
        Assert.NotNull(savedEvent);
        Assert.Contains("Tag 'cleanuperr-done' added", savedEvent.Message);
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
        _notificationPublisherMock.Verify(n => n.NotifyCategoryChanged("old", "new", true), Times.Once);
    }

    #endregion
}
