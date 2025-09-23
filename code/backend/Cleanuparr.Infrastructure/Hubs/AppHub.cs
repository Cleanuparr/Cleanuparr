using Cleanuparr.Infrastructure.Logging;
using Cleanuparr.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Hubs;

/// <summary>
/// Unified SignalR hub for logs and events
/// </summary>
public class AppHub : Hub
{
    private readonly EventsContext _context;
    private readonly ILogger<AppHub> _logger;
    private readonly SignalRLogSink _logSink;

    public AppHub(EventsContext context, ILogger<AppHub> logger)
    {
        _context = context;
        _logger = logger;
        _logSink = SignalRLogSink.Instance;
    }

    /// <summary>
    /// Client requests recent logs
    /// </summary>
    public async Task GetRecentLogs()
    {
        try 
        {
            var logs = _logSink.GetRecentLogs();
            await Clients.Caller.SendAsync("LogsReceived", logs);
            _logger.LogDebug("Sent {count} recent logs to client {connectionId}", logs.Count(), Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send recent logs to client {connectionId}", Context.ConnectionId);
        }
    }

    /// <summary>
    /// Client requests recent events
    /// </summary>
    public async Task GetRecentEvents(int count = 10)
    {
        try
        {
            var events = await _context.Events
                .OrderByDescending(e => e.Timestamp)
                .Take(Math.Min(count, 100)) // Cap at 100
                .ToListAsync();

            await Clients.Caller.SendAsync("EventsReceived", events);
            _logger.LogDebug("Sent {count} recent events to client {connectionId}", events.Count, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send recent events to client {connectionId}", Context.ConnectionId);
        }
    }

    /// <summary>
    /// Client connection established
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogTrace("Client connected to AppHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Client disconnected
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogTrace("Client disconnected from AppHub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
