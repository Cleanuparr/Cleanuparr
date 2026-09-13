using Cleanuparr.Api.Features.Events.Contracts.Responses;
using Cleanuparr.Api.Features.Logging.Contracts.Responses;
using Cleanuparr.Api.Features.Strikes.Contracts.Responses;
using Cleanuparr.Infrastructure.Logging;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Hubs;

/// <summary>
/// Unified hub for logs and events
/// </summary>
public class AppHub : Hub
{
    private readonly ILogger<AppHub> _logger;
    private readonly EventsContext _context;
    private readonly IJobManagementService _jobManagementService;
    private readonly RealtimeLogSink _logSink;
    private readonly AppStatusSnapshot _statusSnapshot;

    public AppHub(EventsContext context, ILogger<AppHub> logger, AppStatusSnapshot statusSnapshot, IJobManagementService jobManagementService)
    {
        _context = context;
        _logger = logger;
        _statusSnapshot = statusSnapshot;
        _jobManagementService = jobManagementService;
        _logSink = RealtimeLogSink.Instance;
    }

    /// <summary>
    /// Client requests recent logs
    /// </summary>
    public async Task GetRecentLogs()
    {
        try
        {
            List<LogEntryResponse> logs = _logSink.GetRecentLogs()
                .Select(LogEntryResponse.From)
                .ToList();

            await Clients.Caller.SendAsync("LogsReceived", logs);
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
            List<EventListItem> events = await _context.Events
                .OrderByDescending(e => e.Timestamp)
                .Take(Math.Min(count, 100)) // Cap at 100
                .Select(EventListItem.FromEvent)
                .ToListAsync();

            await Clients.Caller.SendAsync("EventsReceived", events);
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
            List<ManualEvent> manualEvents = await _context.ManualEvents
                .Where(e => !e.IsResolved)
                .OrderBy(e => e.Timestamp) // Oldest first
                .Take(Math.Min(count, 100)) // Cap at 100
                .ToListAsync();

            await Clients.Caller.SendAsync("ManualEventsReceived", manualEvents.Select(ManualEventResponse.From).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send recent manual events to client");
        }
    }

    /// <summary>
    /// Client requests recent strikes
    /// </summary>
    public async Task GetRecentStrikes(int count = 5)
    {
        try
        {
            List<RecentStrikeDto> strikes = await _context.Strikes
                .Include(s => s.DownloadItem)
                .OrderByDescending(s => s.CreatedAt)
                .Take(Math.Min(count, 50))
                .Select(RecentStrikeDto.FromStrike)
                .ToListAsync();

            await Clients.Caller.SendAsync("StrikesReceived", strikes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send recent strikes to client");
        }
    }

    /// <summary>
    /// Client requests current job statuses
    /// </summary>
    public async Task GetJobStatus()
    {
        try
        {
            IReadOnlyList<JobInfo> jobs = await _jobManagementService.GetAllJobs();
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
        AppStatus status = _statusSnapshot.Current;
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
        await base.OnDisconnectedAsync(exception);
    }
}
