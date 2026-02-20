using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Health;

/// <summary>
/// Service for checking the health of download clients and arr instances
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly Dictionary<Guid, HealthStatus> _healthStatuses = new();
    private readonly Dictionary<Guid, ArrHealthStatus> _arrHealthStatuses = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lockObject = new();

    /// <summary>
    /// Occurs when a client's health status changes
    /// </summary>
    public event EventHandler<ClientHealthChangedEventArgs>? ClientHealthChanged;

    public HealthCheckService(
        ILogger<HealthCheckService> logger,
        IServiceScopeFactory scopeFactory
    )
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public async Task<HealthStatus> CheckClientHealthAsync(Guid clientId)
    {
        _logger.LogDebug("Checking health for client {clientId}", clientId);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
            
            // Get the client configuration
            var downloadClientConfig = await dataContext.DownloadClients
                .Where(x => x.Id == clientId)
                .FirstOrDefaultAsync();
            
            if (downloadClientConfig is null)
            {
                _logger.LogWarning("Client {clientId} not found in configuration", clientId);
                var notFoundStatus = new HealthStatus
                {
                    ClientId = clientId,
                    IsHealthy = false,
                    LastChecked = DateTime.UtcNow,
                    ErrorMessage = "Client not found in configuration"
                };
                
                UpdateHealthStatus(notFoundStatus);
                return notFoundStatus;
            }

            // Get the client instance
            var downloadServiceFactory = scope.ServiceProvider.GetRequiredService<IDownloadServiceFactory>();
            var client = downloadServiceFactory.GetDownloadService(downloadClientConfig);
            
            // Execute the health check
            var healthResult = await client.HealthCheckAsync();
            
            // Create health status object
            var status = new HealthStatus
            {
                ClientId = clientId,
                ClientName = downloadClientConfig.Name,
                ClientTypeName = downloadClientConfig.TypeName,
                IsHealthy = healthResult.IsHealthy,
                LastChecked = DateTime.UtcNow,
                ErrorMessage = healthResult.ErrorMessage,
                ResponseTime = healthResult.ResponseTime
            };
            
            UpdateHealthStatus(status);
            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing health check for client {clientId}", clientId);
            
            var status = new HealthStatus
            {
                ClientId = clientId,
                IsHealthy = false,
                LastChecked = DateTime.UtcNow,
                ErrorMessage = $"Error: {ex.Message}"
            };
            
            UpdateHealthStatus(status);
            return status;
        }
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, HealthStatus>> CheckAllClientsHealthAsync()
    {
        _logger.LogDebug("Checking health for all enabled clients");
        
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
            
            // Get all enabled client configurations
            var enabledClients = await dataContext.DownloadClients
                .Where(x => x.Enabled)
                .ToListAsync();
            var results = new Dictionary<Guid, HealthStatus>();
            
            // Check health of each enabled client
            foreach (var clientConfig in enabledClients)
            {
                var status = await CheckClientHealthAsync(clientConfig.Id);
                results[clientConfig.Id] = status;
            }
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health for all clients");
            return new Dictionary<Guid, HealthStatus>();
        }
    }

    /// <inheritdoc />
    public HealthStatus? GetClientHealth(Guid clientId)
    {
        lock (_lockObject)
        {
            return _healthStatuses.TryGetValue(clientId, out var status) ? status : null;
        }
    }

    /// <inheritdoc />
    public IDictionary<Guid, HealthStatus> GetAllClientHealth()
    {
        lock (_lockObject)
        {
            return new Dictionary<Guid, HealthStatus>(_healthStatuses);
        }
    }

    /// <inheritdoc />
    public async Task<ArrHealthStatus> CheckArrInstanceHealthAsync(Guid instanceId)
    {
        _logger.LogDebug("Checking health for arr instance {instanceId}", instanceId);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            // Get the arr instance with its config (needed for InstanceType)
            var arrInstance = await dataContext.ArrConfigs
                .Include(x => x.Instances)
                .SelectMany(c => c.Instances.Select(i => new { Instance = i, Config = c }))
                .Where(x => x.Instance.Id == instanceId)
                .FirstOrDefaultAsync();

            if (arrInstance is null)
            {
                _logger.LogWarning("Arr instance {instanceId} not found in configuration", instanceId);
                var notFoundStatus = new ArrHealthStatus
                {
                    InstanceId = instanceId,
                    IsHealthy = false,
                    LastChecked = DateTime.UtcNow,
                    ErrorMessage = "Arr instance not found in configuration"
                };

                UpdateArrHealthStatus(notFoundStatus);
                return notFoundStatus;
            }

            // Get the arr client and execute health check
            var arrClientFactory = scope.ServiceProvider.GetRequiredService<IArrClientFactory>();
            var client = arrClientFactory.GetClient(arrInstance.Config.Type, arrInstance.Instance.Version);
            await client.HealthCheckAsync(arrInstance.Instance);

            var status = new ArrHealthStatus
            {
                InstanceId = instanceId,
                InstanceName = arrInstance.Instance.Name,
                InstanceType = arrInstance.Config.Type,
                IsHealthy = true,
                LastChecked = DateTime.UtcNow
            };

            UpdateArrHealthStatus(status);
            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing health check for arr instance {instanceId}", instanceId);

            var status = new ArrHealthStatus
            {
                InstanceId = instanceId,
                IsHealthy = false,
                LastChecked = DateTime.UtcNow,
                ErrorMessage = $"Error: {ex.Message}"
            };

            UpdateArrHealthStatus(status);
            return status;
        }
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, ArrHealthStatus>> CheckAllArrInstancesHealthAsync()
    {
        _logger.LogDebug("Checking health for all enabled arr instances");

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            // Get all enabled arr instances across all configs
            var enabledInstances = await dataContext.ArrConfigs
                .Include(x => x.Instances)
                .SelectMany(c => c.Instances
                    .Where(i => i.Enabled)
                    .Select(i => new { Instance = i, Config = c }))
                .ToListAsync();

            var results = new Dictionary<Guid, ArrHealthStatus>();
            var arrClientFactory = scope.ServiceProvider.GetRequiredService<IArrClientFactory>();

            foreach (var entry in enabledInstances)
            {
                try
                {
                    var client = arrClientFactory.GetClient(entry.Config.Type, entry.Instance.Version);
                    await client.HealthCheckAsync(entry.Instance);

                    var status = new ArrHealthStatus
                    {
                        InstanceId = entry.Instance.Id,
                        InstanceName = entry.Instance.Name,
                        InstanceType = entry.Config.Type,
                        IsHealthy = true,
                        LastChecked = DateTime.UtcNow
                    };

                    UpdateArrHealthStatus(status);
                    results[entry.Instance.Id] = status;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error performing health check for arr instance {instanceId} ({instanceName})",
                        entry.Instance.Id, entry.Instance.Name);

                    var status = new ArrHealthStatus
                    {
                        InstanceId = entry.Instance.Id,
                        InstanceName = entry.Instance.Name,
                        InstanceType = entry.Config.Type,
                        IsHealthy = false,
                        LastChecked = DateTime.UtcNow,
                        ErrorMessage = $"Error: {ex.Message}"
                    };

                    UpdateArrHealthStatus(status);
                    results[entry.Instance.Id] = status;
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health for all arr instances");
            return new Dictionary<Guid, ArrHealthStatus>();
        }
    }

    /// <inheritdoc />
    public ArrHealthStatus? GetArrInstanceHealth(Guid instanceId)
    {
        lock (_lockObject)
        {
            return _arrHealthStatuses.TryGetValue(instanceId, out var status) ? status : null;
        }
    }

    /// <inheritdoc />
    public IDictionary<Guid, ArrHealthStatus> GetAllArrInstanceHealth()
    {
        lock (_lockObject)
        {
            return new Dictionary<Guid, ArrHealthStatus>(_arrHealthStatuses);
        }
    }

    private void UpdateArrHealthStatus(ArrHealthStatus newStatus)
    {
        ArrHealthStatus? previousStatus;

        lock (_lockObject)
        {
            _arrHealthStatuses.TryGetValue(newStatus.InstanceId, out previousStatus);
            _arrHealthStatuses[newStatus.InstanceId] = newStatus;
        }

        bool isStateChange = previousStatus == null ||
                             previousStatus.IsHealthy != newStatus.IsHealthy;

        if (isStateChange)
        {
            _logger.LogInformation(
                "Arr instance {instanceId} ({instanceName}) health changed: {status}",
                newStatus.InstanceId,
                newStatus.InstanceName,
                newStatus.IsHealthy ? "Healthy" : "Unhealthy");
        }
    }

    private void UpdateHealthStatus(HealthStatus newStatus)
    {
        HealthStatus? previousStatus;
        
        lock (_lockObject)
        {
            // Get previous status for comparison
            _healthStatuses.TryGetValue(newStatus.ClientId, out previousStatus);
            
            // Update status
            _healthStatuses[newStatus.ClientId] = newStatus;
        }
        
        // Determine if there's a significant change
        bool isStateChange = previousStatus == null || 
                             previousStatus.IsHealthy != newStatus.IsHealthy;

        // Raise event if there's a significant change
        if (isStateChange)
        {
            _logger.LogInformation(
                "Client {clientId} health changed: {status}", 
                newStatus.ClientId, 
                newStatus.IsHealthy ? "Healthy" : "Unhealthy");
            
            OnClientHealthChanged(new ClientHealthChangedEventArgs(
                newStatus.ClientId, 
                newStatus, 
                previousStatus));
        }
    }
    
    private void OnClientHealthChanged(ClientHealthChangedEventArgs e)
    {
        ClientHealthChanged?.Invoke(this, e);
    }
}
