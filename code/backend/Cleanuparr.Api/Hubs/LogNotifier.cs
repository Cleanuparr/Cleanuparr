using Cleanuparr.Api.Features.Logging.Contracts.Responses;
using Cleanuparr.Infrastructure.Logging;
using Cleanuparr.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Cleanuparr.Api.Hubs;

/// <inheritdoc cref="ILogNotifier" />
public sealed class LogNotifier : ILogNotifier
{
    private readonly IHubContext<AppHub> _hubContext;

    public LogNotifier(IHubContext<AppHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public void NotifyLog(LogEntry entry)
    {
        _ = _hubContext.Clients.All.SendAsync("LogReceived", LogEntryResponse.From(entry));
    }
}
