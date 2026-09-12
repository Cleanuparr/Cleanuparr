using Cleanuparr.Infrastructure.Models;

namespace Cleanuparr.Infrastructure.Realtime;

/// <summary>
/// Pushes application and job status updates to connected clients.
/// </summary>
public interface IStatusNotifier
{
    /// <summary>
    /// Pushes the current application version status.
    /// </summary>
    Task NotifyAppStatusAsync(AppStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals that search statistics changed.
    /// </summary>
    Task NotifySearchStatsUpdatedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals that custom format scores changed.
    /// </summary>
    Task NotifyCustomFormatScoresUpdatedAsync(CancellationToken cancellationToken = default);
}
