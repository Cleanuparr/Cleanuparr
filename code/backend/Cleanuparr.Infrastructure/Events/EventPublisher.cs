using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Infrastructure.Interceptors;
using Infrastructure.Verticals.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Events;

/// <summary>
/// Service for publishing events to database and SignalR hub
/// </summary>
public class EventPublisher
{
    private readonly EventsContext _context;
    private readonly IHubContext<AppHub> _appHubContext;
    private readonly ILogger<EventPublisher> _logger;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IDryRunInterceptor _dryRunInterceptor;

    public EventPublisher(
        EventsContext context, 
        IHubContext<AppHub> appHubContext,
        ILogger<EventPublisher> logger,
        INotificationPublisher notificationPublisher,
        IDryRunInterceptor dryRunInterceptor)
    {
        _context = context;
        _appHubContext = appHubContext;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
        _dryRunInterceptor = dryRunInterceptor;
    }

    /// <summary>
    /// Generic method for publishing events to database and SignalR clients
    /// </summary>
    public async Task PublishAsync(EventType eventType, string message, EventSeverity severity, object? data = null, Guid? trackingId = null)
    {
        var eventEntity = new AppEvent
        {
            EventType = eventType,
            Message = message,
            Severity = severity,
            Data = data != null ? JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            }) : null,
            TrackingId = trackingId
        };

        // Save to database with dry run interception
        await _dryRunInterceptor.InterceptAsync(SaveEventToDatabase, eventEntity);

        // Always send to SignalR clients (not affected by dry run)
        await NotifyClientsAsync(eventEntity);

        _logger.LogTrace("Published event: {eventType}", eventType);
    }

    /// <summary>
    /// Publishes a strike event with context data and notifications
    /// </summary>
    public async Task PublishStrike(StrikeType strikeType, int strikeCount, string hash, string itemName)
    {
        // Determine the appropriate EventType based on StrikeType
        EventType eventType = strikeType switch
        {
            StrikeType.Stalled => EventType.StalledStrike,
            StrikeType.DownloadingMetadata => EventType.DownloadingMetadataStrike,
            StrikeType.FailedImport => EventType.FailedImportStrike,
            StrikeType.SlowSpeed => EventType.SlowSpeedStrike,
            StrikeType.SlowTime => EventType.SlowTimeStrike,
        };

        dynamic data;

        if (strikeType is StrikeType.FailedImport)
        {
            QueueRecord record = ContextProvider.Get<QueueRecord>(nameof(QueueRecord));
            data = new
            {
                hash,
                itemName,
                strikeCount,
                strikeType,
                failedImportReasons = record.StatusMessages ?? [],
            };
        }
        else
        {
            data = new
            {
                hash,
                itemName,
                strikeCount,
                strikeType,
            };
        }

        // Publish the event
        await PublishAsync(
            eventType,
            $"Item '{itemName}' has been struck {strikeCount} times for reason '{strikeType}'",
            EventSeverity.Important,
            data: data);

        // Send notification (uses ContextProvider internally)
        await _notificationPublisher.NotifyStrike(strikeType, strikeCount);
    }

    /// <summary>
    /// Publishes a queue item deleted event with context data and notifications
    /// </summary>
    public async Task PublishQueueItemDeleted(bool removeFromClient, DeleteReason deleteReason)
    {
        // Get context data for the event
        string downloadName = ContextProvider.Get<string>("downloadName") ?? "Unknown";
        string hash = ContextProvider.Get<string>("hash") ?? "Unknown";

        // Publish the event
        await PublishAsync(
            EventType.QueueItemDeleted,
            $"Deleting item from queue with reason: {deleteReason}",
            EventSeverity.Important,
            data: new { downloadName, hash, removeFromClient, deleteReason });

        // Send notification (uses ContextProvider internally)
        await _notificationPublisher.NotifyQueueItemDeleted(removeFromClient, deleteReason);
    }

    /// <summary>
    /// Publishes a download cleaned event with context data and notifications
    /// </summary>
    public async Task PublishDownloadCleaned(double ratio, TimeSpan seedingTime, string categoryName, CleanReason reason)
    {
        // Get context data for the event
        string downloadName = ContextProvider.Get<string>("downloadName");
        string hash = ContextProvider.Get<string>("hash");

        // Publish the event
        await PublishAsync(
            EventType.DownloadCleaned,
            $"Cleaned item from download client with reason: {reason}",
            EventSeverity.Important,
            data: new { downloadName, hash, categoryName, ratio, seedingTime = seedingTime.TotalHours, reason });

        // Send notification (uses ContextProvider internally)
        await _notificationPublisher.NotifyDownloadCleaned(ratio, seedingTime, categoryName, reason);
    }

    /// <summary>
    /// Publishes a category changed event with context data and notifications
    /// </summary>
    public async Task PublishCategoryChanged(string oldCategory, string newCategory, bool isTag = false)
    {
        // Get context data for the event
        string downloadName = ContextProvider.Get<string>("downloadName") ?? "Unknown";
        string hash = ContextProvider.Get<string>("hash") ?? "Unknown";

        // Publish the event
        await PublishAsync(
            EventType.CategoryChanged,
            isTag ? $"Tag '{newCategory}' added to download" : $"Category changed from '{oldCategory}' to '{newCategory}'",
            EventSeverity.Information,
            data: new { downloadName, hash, oldCategory, newCategory, isTag });

        // Send notification (uses ContextProvider internally)
        await _notificationPublisher.NotifyCategoryChanged(oldCategory, newCategory, isTag);
    }

    private async Task SaveEventToDatabase(AppEvent eventEntity)
    {
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();
    }

    private async Task NotifyClientsAsync(AppEvent appEventEntity)
    {
        try
        {
            // Send to all connected clients via the unified AppHub
            await _appHubContext.Clients.All.SendAsync("EventReceived", appEventEntity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event {eventId} to SignalR clients", appEventEntity.Id);
        }
    }
} 