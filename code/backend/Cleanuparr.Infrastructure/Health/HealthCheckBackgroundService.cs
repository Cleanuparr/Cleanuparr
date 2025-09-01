using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Health;

/// <summary>
/// Background service that periodically checks the health of all download clients
/// </summary>
public class HealthCheckBackgroundService : BackgroundService
{
    private readonly ILogger<HealthCheckBackgroundService> _logger;
    private readonly IHealthCheckService _healthCheckService;
    private readonly TimeSpan _checkInterval;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckBackgroundService"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="healthCheckService">The health check service</param>
    public HealthCheckBackgroundService(
        ILogger<HealthCheckBackgroundService> logger,
        IHealthCheckService healthCheckService)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
        
        _checkInterval = TimeSpan.FromMinutes(5);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Performing periodic health check for all download clients");
                
                try
                {
                    // Check health of all clients
                    var results = await _healthCheckService.CheckAllClientsHealthAsync();
                    
                    // Log summary
                    var healthyCount = results.Count(r => r.Value.IsHealthy);
                    var unhealthyCount = results.Count - healthyCount;

                    if (unhealthyCount is 0)
                    {
                        _logger.LogDebug(
                            "Health check completed. {healthyCount} healthy, {unhealthyCount} unhealthy download clients",
                            healthyCount,
                            unhealthyCount);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Health check completed. {healthyCount} healthy, {unhealthyCount} unhealthy download clients",
                            healthyCount,
                            unhealthyCount);
                    }
                    
                    // Log detailed information for unhealthy clients
                    foreach (var result in results.Where(r => !r.Value.IsHealthy))
                    {
                        _logger.LogWarning(
                            "Download client {clientId} ({clientName}) is unhealthy: {errorMessage}",
                            result.Key,
                            result.Value.ClientName,
                            result.Value.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error performing periodic health check");
                }

                // Wait for the next check interval
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown, no need to log error
            _logger.LogInformation("Health check background service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in health check background service");
        }
        finally
        {
            _logger.LogInformation("Health check background service stopped");
        }
    }
}
