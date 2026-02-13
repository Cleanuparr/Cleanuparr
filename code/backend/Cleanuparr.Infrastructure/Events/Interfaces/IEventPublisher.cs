using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Events.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync(EventType eventType, string message, EventSeverity severity, object? data = null, Guid? trackingId = null, Guid? strikeId = null);

    Task PublishManualAsync(string message, EventSeverity severity, object? data = null);

    Task PublishStrike(StrikeType strikeType, int strikeCount, string hash, string itemName, Guid? strikeId = null);

    Task PublishQueueItemDeleted(bool removeFromClient, DeleteReason deleteReason);

    Task PublishDownloadCleaned(double ratio, TimeSpan seedingTime, string categoryName, CleanReason reason);

    Task PublishCategoryChanged(string oldCategory, string newCategory, bool isTag = false);

    Task PublishRecurringItem(string hash, string itemName, int strikeCount);

    Task PublishSearchNotTriggered(string hash, string itemName);
}