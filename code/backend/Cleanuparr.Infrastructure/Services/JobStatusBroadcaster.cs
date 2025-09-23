using System.Threading.RateLimiting;
using Cleanuparr.Infrastructure.Hubs;
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
    private readonly FixedWindowRateLimiter _limiter = new(new FixedWindowRateLimiterOptions
    {
        PermitLimit = 1,
        Window = TimeSpan.FromMinutes(5),
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    });

    /// <summary>
    /// Initializes a new instance of the <see cref="JobStatusBroadcaster"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="jobManagementService">The job management service</param>
    /// <param name="hubContext">The SignalR hub context</param>
    public JobStatusBroadcaster(
        ILogger<JobStatusBroadcaster> logger,
        IJobManagementService jobManagementService,
        IHubContext<AppHub> hubContext
    )
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
        _logger.LogTrace("Job Status Broadcaster started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobs = await _jobManagementService.GetAllJobs();
                await _hubContext.Clients.All.SendAsync("JobStatusUpdate", jobs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when the service is being stopped
                break;
            }
            catch (Exception ex)
            {
                if (_limiter.AttemptAcquire().IsAcquired)
                {
                    _logger.LogError(ex, "Error occurred while broadcasting job status");
                }
            }
            
            await Task.Delay(_broadcastInterval, stoppingToken);
        }

        _logger.LogTrace("Job Status Broadcaster stopped");
    }
}