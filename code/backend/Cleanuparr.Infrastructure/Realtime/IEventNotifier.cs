using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Events;

namespace Cleanuparr.Infrastructure.Realtime;

/// <summary>
/// Pushes events and strikes to connected clients.
/// </summary>
public interface IEventNotifier
{
    /// <summary>
    /// Pushes a newly published event.
    /// </summary>
    Task NotifyEventAsync(AppEvent appEvent);

    /// <summary>
    /// Pushes a newly published manual event.
    /// </summary>
    Task NotifyManualEventAsync(ManualEvent manualEvent);

    /// <summary>
    /// Pushes a strike that was just recorded.
    /// </summary>
    Task NotifyStrikeAsync(Guid strikeId, StrikeType strikeType, string downloadId, string itemTitle, bool isDryRun);
}
