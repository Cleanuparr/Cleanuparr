using Cleanuparr.Infrastructure.Realtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Health;

/// <summary>
/// Service that broadcasts health status changes to connected clients
/// </summary>
public class HealthStatusBroadcaster : IHostedService
{
    private readonly ILogger<HealthStatusBroadcaster> _logger;
    private readonly IHealthCheckService _healthCheckService;
    private readonly IHealthNotifier _healthNotifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthStatusBroadcaster"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="healthCheckService">The health check service</param>
    /// <param name="healthNotifier">The health notifier</param>
    public HealthStatusBroadcaster(
        ILogger<HealthStatusBroadcaster> logger,
        IHealthCheckService healthCheckService,
        IHealthNotifier healthNotifier)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
        _healthNotifier = healthNotifier;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Health status broadcaster starting");
        
        // Subscribe to health status change events
        _healthCheckService.ClientHealthChanged += OnClientHealthChanged;
        _healthCheckService.ClientHealthRemoved += OnClientHealthRemoved;
        _healthCheckService.ArrInstanceHealthRemoved += OnArrInstanceHealthRemoved;
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Health status broadcaster stopping");
        
        // Unsubscribe from health status change events
        _healthCheckService.ClientHealthChanged -= OnClientHealthChanged;
        _healthCheckService.ClientHealthRemoved -= OnClientHealthRemoved;
        _healthCheckService.ArrInstanceHealthRemoved -= OnArrInstanceHealthRemoved;
        
        return Task.CompletedTask;
    }
    
    private async void OnArrInstanceHealthRemoved(object? sender, ArrInstanceHealthRemovedEventArgs e)
    {
        try
        {
            _logger.LogDebug("Broadcasting health status removal for arr instance {InstanceId}", e.InstanceId);

            await _healthNotifier.NotifyArrInstanceRemovedAsync(e.InstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting health status removal for arr instance {InstanceId}", e.InstanceId);
        }
    }

    private async void OnClientHealthRemoved(object? sender, ClientHealthRemovedEventArgs e)
    {
        try
        {
            _logger.LogDebug("Broadcasting health status removal for client {ClientId}", e.ClientId);

            await _healthNotifier.NotifyClientRemovedAsync(e.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting health status removal for client {ClientId}", e.ClientId);
        }
    }

    private async void OnClientHealthChanged(object? sender, ClientHealthChangedEventArgs e)
    {
        try
        {
            _logger.LogDebug("Broadcasting health status change for client {ClientId}", e.ClientId);
            
            // Broadcast to all clients
            await _healthNotifier.NotifyHealthStatusChangedAsync(e.Status);
            
            // Send degradation messages
            if (e.IsDegraded)
            {
                _logger.LogWarning("Client {ClientId} health degraded", e.ClientId);
                await _healthNotifier.NotifyClientDegradedAsync(e.Status);
            }
            
            // Send recovery messages
            if (e.IsRecovered)
            {
                _logger.LogInformation("Client {ClientId} health recovered", e.ClientId);
                await _healthNotifier.NotifyClientRecoveredAsync(e.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting health status change for client {ClientId}", e.ClientId);
        }
    }
}
