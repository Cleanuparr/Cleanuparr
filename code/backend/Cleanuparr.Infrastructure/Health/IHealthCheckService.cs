namespace Cleanuparr.Infrastructure.Health;

/// <summary>
/// Service for checking the health of download clients and arr instances
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Occurs when a client's health status changes
    /// </summary>
    event EventHandler<ClientHealthChangedEventArgs> ClientHealthChanged;

    /// <summary>
    /// Checks the health of a specific download client
    /// </summary>
    /// <param name="clientId">The client ID to check</param>
    /// <returns>The health status of the client</returns>
    Task<HealthStatus> CheckClientHealthAsync(Guid clientId);

    /// <summary>
    /// Checks the health of all enabled download clients
    /// </summary>
    /// <returns>A dictionary of client IDs to health statuses</returns>
    Task<IDictionary<Guid, HealthStatus>> CheckAllClientsHealthAsync();

    /// <summary>
    /// Gets the current health status of a download client
    /// </summary>
    /// <param name="clientId">The client ID</param>
    /// <returns>The current health status, or null if the client hasn't been checked</returns>
    HealthStatus? GetClientHealth(Guid clientId);

    /// <summary>
    /// Gets the current health status of all download clients that have been checked
    /// </summary>
    /// <returns>A dictionary of client IDs to health statuses</returns>
    IDictionary<Guid, HealthStatus> GetAllClientHealth();

    /// <summary>
    /// Checks the health of a specific arr instance
    /// </summary>
    /// <param name="instanceId">The arr instance ID to check</param>
    /// <returns>The health status of the arr instance</returns>
    Task<ArrHealthStatus> CheckArrInstanceHealthAsync(Guid instanceId);

    /// <summary>
    /// Checks the health of all enabled arr instances
    /// </summary>
    /// <returns>A dictionary of instance IDs to health statuses</returns>
    Task<IDictionary<Guid, ArrHealthStatus>> CheckAllArrInstancesHealthAsync();

    /// <summary>
    /// Gets the current health status of an arr instance
    /// </summary>
    /// <param name="instanceId">The arr instance ID</param>
    /// <returns>The current health status, or null if the instance hasn't been checked</returns>
    ArrHealthStatus? GetArrInstanceHealth(Guid instanceId);

    /// <summary>
    /// Gets the current health status of all arr instances that have been checked
    /// </summary>
    /// <returns>A dictionary of instance IDs to health statuses</returns>
    IDictionary<Guid, ArrHealthStatus> GetAllArrInstanceHealth();
}
