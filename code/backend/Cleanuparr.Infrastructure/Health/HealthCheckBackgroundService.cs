using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Health;

/// <summary>
/// Background service that periodically checks the health of all download clients and arr instances
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
                _logger.LogDebug("Performing periodic health check for all download clients and arr instances");

                try
                {
                    // Check health of all download clients
                    var clientResults = await _healthCheckService.CheckAllClientsHealthAsync();

                    var clientHealthy = clientResults.Count(r => r.Value.IsHealthy);
                    var clientUnhealthy = clientResults.Count - clientHealthy;

                    if (clientUnhealthy is 0)
                    {
                        _logger.LogDebug(
                            "Download client health check completed. {healthyCount} healthy, {unhealthyCount} unhealthy",
                            clientHealthy,
                            clientUnhealthy);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Download client health check completed. {healthyCount} healthy, {unhealthyCount} unhealthy",
                            clientHealthy,
                            clientUnhealthy);
                    }

                    foreach (var result in clientResults.Where(r => !r.Value.IsHealthy))
                    {
                        _logger.LogWarning(
                            "Download client {clientId} ({clientName}) is unhealthy: {errorMessage}",
                            result.Key,
                            result.Value.ClientName,
                            result.Value.ErrorMessage);
                    }

                    // Check health of all arr instances
                    var arrResults = await _healthCheckService.CheckAllArrInstancesHealthAsync();

                    var arrHealthy = arrResults.Count(r => r.Value.IsHealthy);
                    var arrUnhealthy = arrResults.Count - arrHealthy;

                    if (arrUnhealthy is 0)
                    {
                        _logger.LogDebug(
                            "Arr instance health check completed. {healthyCount} healthy, {unhealthyCount} unhealthy",
                            arrHealthy,
                            arrUnhealthy);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Arr instance health check completed. {healthyCount} healthy, {unhealthyCount} unhealthy",
                            arrHealthy,
                            arrUnhealthy);
                    }

                    foreach (var result in arrResults.Where(r => !r.Value.IsHealthy))
                    {
                        _logger.LogWarning(
                            "Arr instance {instanceId} ({instanceName}) is unhealthy: {errorMessage}",
                            result.Key,
                            result.Value.InstanceName,
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
