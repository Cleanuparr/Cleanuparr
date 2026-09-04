using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Events;

namespace Cleanuparr.Infrastructure.Events.Interfaces;

public interface IEventPublisher
{
    /// <summary>
    /// Saves an event and sends it to the connected clients.
    /// </summary>
    Task PublishAsync(EventType eventType, string message, EventSeverity severity, Action<AppEvent>? configure = null, Guid? trackingId = null, Guid? strikeId = null, bool? isDryRun = null);

    /// <summary>
    /// Saves a manual event if no duplicate is open, then sends it to the connected clients.
    /// </summary>
    Task PublishManualAsync(ManualEventType type, string message, EventSeverity severity, Action<ManualEvent>? configure = null, bool? isDryRun = null);

    /// <summary>
    /// Records a strike for a download and sends a notification.
    /// </summary>
    Task PublishStrike(StrikeType strikeType, int strikeCount, string hash, string itemName, Guid? strikeId = null);

    /// <summary>
    /// Records that a download recovered and that its strikes of this type are clear.
    /// </summary>
    Task PublishStrikeReset(StrikeType strikeType, int strikeCount, string hash, string itemName);

    /// <summary>
    /// Records that a queue item was deleted and sends a notification.
    /// </summary>
    Task PublishQueueItemDeleted(bool removeFromClient, DeleteReason deleteReason);

    /// <summary>
    /// Records that a download was cleaned and sends a notification.
    /// </summary>
    Task PublishDownloadCleaned(double ratio, TimeSpan seedingTime, string categoryName, CleanReason reason);

    /// <summary>
    /// Records that a download was stopped and sends a notification.
    /// </summary>
    Task PublishDownloadStopped(double ratio, TimeSpan seedingTime, string categoryName, CleanReason reason);

    /// <summary>
    /// Records that the category or the tag of a download changed.
    /// </summary>
    Task PublishCategoryChanged(string oldCategory, string newCategory, bool isTag = false);

    /// <summary>
    /// Records that a download came back after its removal.
    /// </summary>
    Task PublishRecurringItem(string hash, string itemName, int strikeCount);

    /// <summary>
    /// Records that no search started for an item.
    /// </summary>
    Task PublishSearchNotTriggered(string hash, string itemName);

    /// <summary>
    /// Records that a search started for an item.
    /// </summary>
    /// <returns>The ID of the new event.</returns>
    Task<Guid> PublishSearchTriggered(string itemTitle, SeekerSearchType searchType, SeekerSearchReason searchReason, Guid? cycleId = null, bool? isDryRun = null);

    /// <summary>
    /// Sets the terminal status of a search event and records the grabbed items.
    /// </summary>
    Task PublishSearchCompleted(Guid eventId, SearchCommandStatus status, InstanceType instanceType, string instanceUrl, List<string>? grabbedItems = null);

    /// <summary>
    /// Sets the status of a search event to started.
    /// </summary>
    Task PublishSearchStarted(Guid eventId);

    /// <summary>
    /// Fails each search event of an arr instance that has no terminal status.
    /// </summary>
    /// <returns>The number of events that failed.</returns>
    Task<int> FailStrandedSearchEvents(Guid arrInstanceId);

    /// <summary>
    /// Fails each search event that has no command tracker and is older than the cutoff.
    /// </summary>
    /// <returns>The number of events that failed.</returns>
    Task<int> FailAbandonedSearchEvents(DateTimeOffset cutoff);
}
