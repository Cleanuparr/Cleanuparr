using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Health;

/// <summary>
/// Service for checking the health of download clients
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly Dictionary<Guid, HealthStatus> _healthStatuses = new();
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
            var downloadServiceFactory = scope.ServiceProvider.GetRequiredService<DownloadServiceFactory>();
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
