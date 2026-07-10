using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Providers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Events;

/// <summary>
/// Service for publishing events to database and SignalR hub
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly EventsContext _context;
    private readonly IHubContext<AppHub> _appHubContext;
    private readonly ILogger<EventPublisher> _logger;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly IDatabaseProvider _databaseProvider;

    public EventPublisher(
        EventsContext context,
        IHubContext<AppHub> appHubContext,
        ILogger<EventPublisher> logger,
        INotificationPublisher notificationPublisher,
        IDryRunInterceptor dryRunInterceptor,
        IDatabaseProvider databaseProvider)
    {
        _context = context;
        _appHubContext = appHubContext;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
        _dryRunInterceptor = dryRunInterceptor;
        _databaseProvider = databaseProvider;
    }

    /// <summary>
    /// Generic method for publishing events to database and SignalR clients.
    /// Common context fields are populated here; <paramref name="configure"/> sets event-type-specific typed fields.
    /// </summary>
    public async Task PublishAsync(EventType eventType, string message, EventSeverity severity, Action<AppEvent>? configure = null, Guid? trackingId = null, Guid? strikeId = null, bool? isDryRun = null)
    {
        AppEvent eventEntity = new()
        {
            EventType = eventType,
            Message = message,
            Severity = severity,
            TrackingId = trackingId,
            StrikeId = strikeId,
            JobRunId = ContextProvider.TryGetJobRunId(),
            ArrInstanceId = ContextProvider.Get(ContextProvider.Keys.ArrInstanceId) as Guid?,
            DownloadClientId = ContextProvider.Get(ContextProvider.Keys.DownloadClientId) as Guid?,
            InstanceType = ContextProvider.Get(nameof(InstanceType)) is InstanceType it ? it : null,
            InstanceUrl = (ContextProvider.Get(ContextProvider.Keys.ArrInstanceUrl) as Uri)?.ToString(),
            DownloadClientType = ContextProvider.Get(ContextProvider.Keys.DownloadClientType) is DownloadClientTypeName dct ? dct : null,
            DownloadClientName = ContextProvider.Get(ContextProvider.Keys.DownloadClientName) as string,
        };

        configure?.Invoke(eventEntity);

        eventEntity.IsDryRun = isDryRun ?? await _dryRunInterceptor.IsDryRunEnabled();

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        await NotifyClientsAsync(eventEntity);

        _logger.LogTrace("Published event: {eventType}", eventType);
    }

    /// <summary>
    /// Publishes a manual event, gated to avoid duplicates. Common context fields are populated here;
    /// <paramref name="configure"/> sets event-type-specific typed fields. When an item hash is set,
    /// the event is suppressed if an unresolved event of the same type/hash already exists, or if one
    /// was resolved within the post-resolve cooldown window.
    /// </summary>
    public async Task PublishManualAsync(ManualEventType type, string message, EventSeverity severity, Action<ManualEvent>? configure = null, bool? isDryRun = null)
    {
        ManualEvent eventEntity = new()
        {
            Type = type,
            Message = message,
            Severity = severity,
            JobRunId = ContextProvider.TryGetJobRunId(),
            InstanceType = ContextProvider.Get(nameof(InstanceType)) is InstanceType it ? it : null,
            InstanceUrl = (ContextProvider.Get(ContextProvider.Keys.ArrInstanceUrl) as Uri)?.ToString(),
            DownloadClientType = ContextProvider.Get(ContextProvider.Keys.DownloadClientType) is DownloadClientTypeName dct ? dct : null,
            DownloadClientName = ContextProvider.Get(ContextProvider.Keys.DownloadClientName) as string,
        };

        configure?.Invoke(eventEntity);

        string? normalizedHash = eventEntity.ItemHash?.ToLowerInvariant();
        eventEntity.ItemHash = normalizedHash;

        if (normalizedHash is not null)
        {
            // ponytail: 1h cooldown is hardcoded by request; make it a config value only if it needs tuning.
            DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddHours(-1);

            // Suppress if an unresolved event already exists (dedup) OR one was resolved < 1h ago (post-resolve cooldown).
            bool suppress = await _context.ManualEvents.AnyAsync(e =>
                e.Type == type &&
                e.ItemHash == normalizedHash &&
                (!e.IsResolved || (e.ResolvedAt != null && e.ResolvedAt >= cutoff)));

            if (suppress)
            {
                _logger.LogDebug("Skipping manual event {type} for {hash} (unresolved or within cooldown)", type, normalizedHash);
                return;
            }
        }

        eventEntity.IsDryRun = isDryRun ?? await _dryRunInterceptor.IsDryRunEnabled();

        try
        {
            _context.ManualEvents.Add(eventEntity);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (normalizedHash is not null
            && _databaseProvider.IsUniqueConstraintViolation(ex))
        {
            _logger.LogDebug("Manual event {type} for {hash} rejected by unique index", type, normalizedHash);
            _context.Entry(eventEntity).State = EntityState.Detached;
            return;
        }

        await NotifyClientsAsync(eventEntity);

        _logger.LogTrace("Published manual event: {message}", message);
    }

    /// <summary>
    /// Publishes a strike event with context data and notifications
    /// </summary>
    public async Task PublishStrike(StrikeType strikeType, int strikeCount, string hash, string itemName, Guid? strikeId = null)
    {
        // Determine the appropriate EventType based on StrikeType
        EventType eventType = strikeType switch
        {
            StrikeType.Stalled => EventType.StalledStrike,
            StrikeType.DownloadingMetadata => EventType.DownloadingMetadataStrike,
            StrikeType.FailedImport => EventType.FailedImportStrike,
            StrikeType.SlowSpeed => EventType.SlowSpeedStrike,
            StrikeType.SlowTime => EventType.SlowTimeStrike,
            StrikeType.DeadTorrent => EventType.DeadTorrentStrike,
            _ => throw new ArgumentOutOfRangeException(nameof(strikeType), strikeType, null)
        };

        List<string> failedImportReasons = [];

        if (strikeType is StrikeType.FailedImport)
        {
            QueueRecord record = ContextProvider.Get<QueueRecord>(nameof(QueueRecord));
            failedImportReasons = record.StatusMessages?
                .Select(m => m.Messages is { Count: > 0 }
                    ? $"{m.Title}: {string.Join("; ", m.Messages)}"
                    : m.Title)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList() ?? [];
        }

        bool isDryRun = await _dryRunInterceptor.IsDryRunEnabled();

        // Publish the event
        await PublishAsync(
            eventType,
            $"Item '{itemName}' has been struck {strikeCount} times for reason '{strikeType}'",
            EventSeverity.Important,
            configure: e =>
            {
                e.ItemTitle = itemName;
                e.ItemHash = hash;
                e.StrikeCount = strikeCount;
                e.FailedImportReasons = failedImportReasons;
            },
            strikeId: strikeId,
            isDryRun: isDryRun);

        // Broadcast strike to SignalR clients for real-time dashboard updates
        await BroadcastStrikeAsync(strikeId, strikeType, hash, itemName, isDryRun);

        // Send notification (uses ContextProvider internally)
        await _notificationPublisher.NotifyStrike(strikeType, strikeCount);
    }

    /// <summary>
    /// Publishes a strike reset event: emitted when a download recovers and its strikes of a given type are cleared.
    /// </summary>
    public async Task PublishStrikeReset(StrikeType strikeType, int strikeCount, string hash, string itemName)
    {
        await PublishAsync(
            EventType.StrikeReset,
            $"'{itemName}' recovered — {strikeCount} '{strikeType}' strike(s) reset",
            EventSeverity.Information,
            configure: e =>
            {
                e.ItemTitle = itemName;
                e.ItemHash = hash;
                e.StrikeCount = strikeCount;
            });
    }

    /// <summary>
    /// Publishes a queue item deleted event with context data and notifications
    /// </summary>
    public async Task PublishQueueItemDeleted(bool removeFromClient, DeleteReason deleteReason)
    {
        // Get context data for the event
        string itemName = ContextProvider.Get<string>(ContextProvider.Keys.ItemName);
        string hash = ContextProvider.Get<string>(ContextProvider.Keys.Hash);

        // Publish the event
        await PublishAsync(
            EventType.QueueItemDeleted,
            $"Deleting item from queue with reason: {deleteReason}",
            EventSeverity.Important,
            configure: e =>
            {
                e.ItemTitle = itemName;
                e.ItemHash = hash;
                e.DeleteReason = deleteReason;
                e.RemoveFromClient = removeFromClient;
            });

        // Send notification (uses ContextProvider internally)
        await _notificationPublisher.NotifyQueueItemDeleted(removeFromClient, deleteReason);
    }

    /// <summary>
    /// Publishes a download cleaned event with context data and notifications
    /// </summary>
    public async Task PublishDownloadCleaned(double ratio, TimeSpan seedingTime, string categoryName, CleanReason reason)
    {
        // Get context data for the event
        string itemName = ContextProvider.Get<string>(ContextProvider.Keys.ItemName);
        string hash = ContextProvider.Get<string>(ContextProvider.Keys.Hash);

        // Publish the event
        await PublishAsync(
            EventType.DownloadCleaned,
            $"Cleaned item from download client with reason: {reason}",
            EventSeverity.Important,
            configure: e =>
            {
                e.ItemTitle = itemName;
                e.ItemHash = hash;
                e.CleanedCategory = categoryName;
                e.SeedRatio = ratio;
                e.SeedingTimeHours = seedingTime.TotalHours;
                e.CleanReason = reason;
            });

        // Send notification (uses ContextProvider internally)
        await _notificationPublisher.NotifyDownloadCleaned(ratio, seedingTime, categoryName, reason);
    }

    /// <summary>
    /// Publishes a category changed event with context data and notifications
    /// </summary>
    public async Task PublishCategoryChanged(string oldCategory, string newCategory, bool isTag = false)
    {
        // Get context data for the event
        string itemName = ContextProvider.Get<string>(ContextProvider.Keys.ItemName);
        string hash = ContextProvider.Get<string>(ContextProvider.Keys.Hash);

        // Publish the event
        await PublishAsync(
            EventType.CategoryChanged,
            isTag ? $"Tag '{newCategory}' added to download" : $"Category changed from '{oldCategory}' to '{newCategory}'",
            EventSeverity.Information,
            configure: e =>
            {
                e.ItemTitle = itemName;
                e.ItemHash = hash;
                e.OldCategory = oldCategory;
                e.NewCategory = newCategory;
                e.IsCategoryTag = isTag;
            });

        // Send notification (uses ContextProvider internally)
        await _notificationPublisher.NotifyCategoryChanged(oldCategory, newCategory, isTag);
    }

    /// <summary>
    /// Publishes an event alerting that an item keeps coming back
    /// </summary>
    public async Task PublishRecurringItem(string hash, string itemName, int strikeCount)
    {
        await PublishManualAsync(
            ManualEventType.RecurringDownload,
            "Download keeps coming back after deletion\nTo prevent further issues, please consult the prerequisites: https://cleanuparr.github.io/Cleanuparr/docs/installation/",
            EventSeverity.Important,
            configure: e =>
            {
                e.ItemTitle = itemName;
                e.ItemHash = hash;
                e.StrikeCount = strikeCount;
            }
        );
    }

    /// <summary>
    /// Publishes a search triggered event with context data and notifications.
    /// Returns the event ID so the SeekerCommandMonitor can update it on completion.
    /// </summary>
    public async Task<Guid> PublishSearchTriggered(string itemTitle, SeekerSearchType searchType, SeekerSearchReason searchReason, Guid? cycleId = null)
    {
        AppEvent eventEntity = new()
        {
            EventType = EventType.SearchTriggered,
            Message = $"Search triggered for {itemTitle}",
            Severity = EventSeverity.Information,
            SearchStatus = SearchCommandStatus.Pending,
            JobRunId = ContextProvider.TryGetJobRunId(),
            ArrInstanceId = ContextProvider.Get(ContextProvider.Keys.ArrInstanceId) as Guid?,
            DownloadClientId = ContextProvider.Get(ContextProvider.Keys.DownloadClientId) as Guid?,
            InstanceType = ContextProvider.Get(nameof(InstanceType)) is InstanceType it ? it : null,
            InstanceUrl = (ContextProvider.Get(ContextProvider.Keys.ArrInstanceUrl) as Uri)?.ToString(),
            DownloadClientType = ContextProvider.Get(ContextProvider.Keys.DownloadClientType) is DownloadClientTypeName dct ? dct : null,
            DownloadClientName = ContextProvider.Get(ContextProvider.Keys.DownloadClientName) as string,
            CycleId = cycleId,
            ItemTitle = itemTitle,
            SearchType = searchType,
            SearchReason = searchReason,
        };

        eventEntity.IsDryRun = await _dryRunInterceptor.IsDryRunEnabled();

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        await NotifyClientsAsync(eventEntity);
        await _notificationPublisher.NotifySearchTriggered(itemTitle, searchType, searchReason);

        return eventEntity.Id;
    }

    /// <summary>
    /// Updates an existing search event with completion status and optional grabbed item titles
    /// </summary>
    public async Task PublishSearchCompleted(Guid eventId, SearchCommandStatus status, InstanceType instanceType, string instanceUrl, List<string>? grabbedItems = null)
    {
        var existingEvent = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (existingEvent is null)
        {
            _logger.LogWarning("Could not find search event {EventId} to update completion status", eventId);
            return;
        }

        existingEvent.SearchStatus = status;
        existingEvent.CompletedAt = DateTimeOffset.UtcNow;

        if (grabbedItems is { Count: > 0 })
        {
            existingEvent.GrabbedItems = grabbedItems;
        }

        await _context.SaveChangesAsync();
        await NotifyClientsAsync(existingEvent);

        if (status is SearchCommandStatus.Completed && grabbedItems is { Count: > 0 })
        {
            await _notificationPublisher.NotifySearchItemGrabbed(existingEvent.ItemTitle ?? string.Empty, grabbedItems, instanceType, instanceUrl);
        }
    }

    /// <summary>
    /// Publishes an event alerting that search was not triggered for an item
    /// </summary>
    public async Task PublishSearchNotTriggered(string hash, string itemName)
    {
        await PublishManualAsync(
            ManualEventType.SearchNotTriggered,
            "Replacement search was not triggered after removal\nPlease trigger a manual search if needed",
            EventSeverity.Warning,
            configure: e =>
            {
                e.ItemTitle = itemName;
                e.ItemHash = hash;
            }
        );
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

    private async Task NotifyClientsAsync(ManualEvent appEventEntity)
    {
        try
        {
            // Send to all connected clients via the unified AppHub
            await _appHubContext.Clients.All.SendAsync("ManualEventReceived", appEventEntity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event {eventId} to SignalR clients", appEventEntity.Id);
        }
    }

    private async Task BroadcastStrikeAsync(Guid? strikeId, StrikeType strikeType, string hash, string itemName, bool isDryRun)
    {
        try
        {
            var strike = new
            {
                Id = strikeId ?? Guid.Empty,
                Type = strikeType.ToString(),
                CreatedAt = DateTimeOffset.UtcNow,
                DownloadId = hash,
                Title = itemName,
                IsDryRun = isDryRun,
            };
            await _appHubContext.Clients.All.SendAsync("StrikeReceived", strike);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send strike to SignalR clients");
        }
    }
}
