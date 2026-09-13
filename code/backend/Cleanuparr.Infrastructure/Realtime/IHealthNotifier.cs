using Cleanuparr.Infrastructure.Health;

namespace Cleanuparr.Infrastructure.Realtime;

/// <summary>
/// Pushes download client and *arr instance health updates to connected clients.
/// </summary>
public interface IHealthNotifier
{
    /// <summary>
    /// Pushes the latest health status of a download client.
    /// </summary>
    Task NotifyHealthStatusChangedAsync(HealthStatus status);

    /// <summary>
    /// Pushes a download client that just became unhealthy.
    /// </summary>
    Task NotifyClientDegradedAsync(HealthStatus status);

    /// <summary>
    /// Pushes a download client that just became healthy again.
    /// </summary>
    Task NotifyClientRecoveredAsync(HealthStatus status);

    /// <summary>
    /// Pushes the id of a download client that left the health cache.
    /// </summary>
    Task NotifyClientRemovedAsync(Guid clientId);

    /// <summary>
    /// Pushes the id of an *arr instance that left the health cache.
    /// </summary>
    Task NotifyArrInstanceRemovedAsync(Guid instanceId);
}
