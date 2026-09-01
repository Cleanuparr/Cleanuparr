using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Health;

/// <summary>
/// Service that broadcasts health status changes via SignalR
/// </summary>
public class HealthStatusBroadcaster : IHostedService
{
    private readonly ILogger<HealthStatusBroadcaster> _logger;
    private readonly IHealthCheckService _healthCheckService;
    private readonly IHubContext<HealthStatusHub> _hubContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthStatusBroadcaster"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="healthCheckService">The health check service</param>
    /// <param name="hubContext">The SignalR hub context</param>
    public HealthStatusBroadcaster(
        ILogger<HealthStatusBroadcaster> logger,
        IHealthCheckService healthCheckService,
        IHubContext<HealthStatusHub> hubContext)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
        _hubContext = hubContext;
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
            _logger.LogDebug("Broadcasting health status removal for arr instance {instanceId}", e.InstanceId);

            await _hubContext.Clients.All.SendAsync("ArrInstanceRemoved", e.InstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting health status removal for arr instance {instanceId}", e.InstanceId);
        }
    }

    private async void OnClientHealthRemoved(object? sender, ClientHealthRemovedEventArgs e)
    {
        try
        {
            _logger.LogDebug("Broadcasting health status removal for client {clientId}", e.ClientId);

            await _hubContext.Clients.All.SendAsync("ClientRemoved", e.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting health status removal for client {clientId}", e.ClientId);
        }
    }

    private async void OnClientHealthChanged(object? sender, ClientHealthChangedEventArgs e)
    {
        try
        {
            _logger.LogDebug("Broadcasting health status change for client {clientId}", e.ClientId);
            
            // Broadcast to all clients
            await _hubContext.Clients.All.SendAsync("HealthStatusChanged", e.Status);
            
            // Send degradation messages
            if (e.IsDegraded)
            {
                _logger.LogWarning("Client {clientId} health degraded", e.ClientId);
                await _hubContext.Clients.All.SendAsync("ClientDegraded", e.Status);
            }
            
            // Send recovery messages
            if (e.IsRecovered)
            {
                _logger.LogInformation("Client {clientId} health recovered", e.ClientId);
                await _hubContext.Clients.All.SendAsync("ClientRecovered", e.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting health status change for client {clientId}", e.ClientId);
        }
    }
}
