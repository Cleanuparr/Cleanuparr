using Microsoft.AspNetCore.SignalR;

namespace Cleanuparr.Api.Hubs;

/// <summary>
/// Hub for broadcasting health status updates
/// </summary>
public class HealthStatusHub : Hub
{
    private readonly ILogger<HealthStatusHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthStatusHub"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    public HealthStatusHub(ILogger<HealthStatusHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {connectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {connectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
