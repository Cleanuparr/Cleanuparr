using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Cleanuparr.Infrastructure.Services.Interfaces;

namespace Cleanuparr.Infrastructure.Services;

/// <summary>
/// Background service that periodically broadcasts job status updates via SignalR
/// </summary>
public class JobStatusBroadcaster : BackgroundService
{
    private readonly ILogger<JobStatusBroadcaster> _logger;
    private readonly IJobManagementService _jobManagementService;
    private readonly IHubContext<Hubs.AppHub> _hubContext;
    private readonly TimeSpan _broadcastInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="JobStatusBroadcaster"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="jobManagementService">The job management service</param>
    /// <param name="hubContext">The SignalR hub context</param>
    public JobStatusBroadcaster(
        ILogger<JobStatusBroadcaster> logger,
        IJobManagementService jobManagementService,
        IHubContext<Hubs.AppHub> hubContext)
    {
        _logger = logger;
        _jobManagementService = jobManagementService;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Executes the background service
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the service</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job Status Broadcaster started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Only broadcast if there are connected clients
                // This is an optimization - SignalR will handle this internally too
                await BroadcastJobStatus();

                // Wait for the next interval
                await Task.Delay(_broadcastInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when the service is being stopped
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while broadcasting job status");
                
                // Wait a bit before retrying to avoid flooding logs on persistent errors
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Job Status Broadcaster stopped");
    }

    /// <summary>
    /// Broadcasts current job status to all connected clients
    /// </summary>
    private async Task BroadcastJobStatus()
    {
        try
        {
            var jobs = await _jobManagementService.GetAllJobs();
            await _hubContext.Clients.All.SendAsync("JobStatusUpdate", jobs);
            
            _logger.LogDebug("Broadcasted job status update to all clients - {JobCount} jobs", jobs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast job status update");
        }
    }
}