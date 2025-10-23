using Cleanuparr.Infrastructure.Logging;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Infrastructure.Services.Interfaces;
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
    private readonly ILogger<AppHub> _logger;
    private readonly EventsContext _context;
    private readonly IJobManagementService _jobManagementService;
    private readonly SignalRLogSink _logSink;
    private readonly AppStatusSnapshot _statusSnapshot;

    public AppHub(EventsContext context, ILogger<AppHub> logger, AppStatusSnapshot statusSnapshot, IJobManagementService jobManagementService)
    {
        _context = context;
        _logger = logger;
        _statusSnapshot = statusSnapshot;
        _jobManagementService = jobManagementService;
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
            _logger.LogError(ex, "Failed to send recent logs to client");
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
            _logger.LogError(ex, "Failed to send recent events to client");
        }
    }

    /// <summary>
    /// Client requests recent manual events
    /// </summary>
    public async Task GetRecentManualEvents(int count = 100)
    {
        try
        {
            var manualEvents = await _context.ManualEvents
                .Where(e => !e.IsResolved)
                .OrderBy(e => e.Timestamp) // Oldest first
                .Take(Math.Min(count, 100)) // Cap at 100
                .ToListAsync();

            await Clients.Caller.SendAsync("ManualEventsReceived", manualEvents);
            _logger.LogDebug("Sent {count} recent manual events to client {connectionId}", manualEvents.Count, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send recent manual events to client");
        }
    }

    /// <summary>
    /// Client requests current job statuses
    /// </summary>
    public async Task GetJobStatus()
    {
        try
        {
            var jobs = await _jobManagementService.GetAllJobs();
            await Clients.All.SendAsync("JobsStatusUpdate", jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send job status to client");
        }
    }

    /// <summary>
    /// Client connection established
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogTrace("Client connected to AppHub: {ConnectionId}", Context.ConnectionId);

        var status = _statusSnapshot.Current;
        if (status.CurrentVersion is not null || status.LatestVersion is not null)
        {
            await Clients.Caller.SendAsync("AppStatusUpdated", status);
        }

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
