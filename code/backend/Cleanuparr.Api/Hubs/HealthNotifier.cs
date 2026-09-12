using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Cleanuparr.Api.Hubs;

/// <inheritdoc cref="IHealthNotifier" />
public sealed class HealthNotifier : IHealthNotifier
{
    private readonly IHubContext<HealthStatusHub> _hubContext;

    public HealthNotifier(IHubContext<HealthStatusHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public Task NotifyHealthStatusChangedAsync(HealthStatus status)
    {
        return _hubContext.Clients.All.SendAsync("HealthStatusChanged", status);
    }

    /// <inheritdoc />
    public Task NotifyClientDegradedAsync(HealthStatus status)
    {
        return _hubContext.Clients.All.SendAsync("ClientDegraded", status);
    }

    /// <inheritdoc />
    public Task NotifyClientRecoveredAsync(HealthStatus status)
    {
        return _hubContext.Clients.All.SendAsync("ClientRecovered", status);
    }

    /// <inheritdoc />
    public Task NotifyClientRemovedAsync(Guid clientId)
    {
        return _hubContext.Clients.All.SendAsync("ClientRemoved", clientId);
    }

    /// <inheritdoc />
    public Task NotifyArrInstanceRemovedAsync(Guid instanceId)
    {
        return _hubContext.Clients.All.SendAsync("ArrInstanceRemoved", instanceId);
    }
}
