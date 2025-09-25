using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Quartz;
using Serilog.Context;

namespace Cleanuparr.Api.Jobs;

[DisallowConcurrentExecution]
public sealed class GenericJob<T> : IJob
    where T : IHandler
{
    private readonly ILogger<GenericJob<T>> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    
    public GenericJob(ILogger<GenericJob<T>> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }
    
    public async Task Execute(IJobExecutionContext context)
    {
        using var _ = LogContext.PushProperty("JobName", typeof(T).Name);
        
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
            var jobManagementService = scope.ServiceProvider.GetRequiredService<IJobManagementService>();
            
            await BroadcastJobStatus(hubContext, jobManagementService, false);
            
            var handler = scope.ServiceProvider.GetRequiredService<T>();
            await handler.ExecuteAsync();
            
            await BroadcastJobStatus(hubContext, jobManagementService, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{name} failed", typeof(T).Name);
        }
    }
    
    private async Task BroadcastJobStatus(IHubContext<AppHub> hubContext, IJobManagementService jobManagementService, bool isFinished)
    {
        try
        {
            JobType jobType = Enum.Parse<JobType>(typeof(T).Name);
            JobInfo jobInfo = await jobManagementService.GetJob(jobType);

            if (isFinished)
            {
                jobInfo.Status = "Scheduled";
            }
            
            await hubContext.Clients.All.SendAsync("JobStatusUpdate", jobInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast job status update");
        }
    }
}