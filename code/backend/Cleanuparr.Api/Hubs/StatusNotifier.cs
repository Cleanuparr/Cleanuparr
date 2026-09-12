using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Cleanuparr.Api.Hubs;

/// <inheritdoc cref="IStatusNotifier" />
public sealed class StatusNotifier : IStatusNotifier
{
    private readonly IHubContext<AppHub> _hubContext;

    public StatusNotifier(IHubContext<AppHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public Task NotifyAppStatusAsync(AppStatus status, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync("AppStatusUpdated", status, cancellationToken);
    }

    /// <inheritdoc />
    public Task NotifySearchStatsUpdatedAsync(CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync("SearchStatsUpdated", cancellationToken);
    }

    /// <inheritdoc />
    public Task NotifyCustomFormatScoresUpdatedAsync(CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync("CfScoresUpdated", cancellationToken);
    }
}
