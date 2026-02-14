using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.State;
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

        Guid jobRunId = Guid.CreateVersion7();
        JobType jobType = Enum.Parse<JobType>(typeof(T).Name);
        JobRunStatus? status = null;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var eventsContext = scope.ServiceProvider.GetRequiredService<EventsContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
            var jobManagementService = scope.ServiceProvider.GetRequiredService<IJobManagementService>();

            var jobRun = new JobRun { Id = jobRunId, Type = jobType };
            eventsContext.JobRuns.Add(jobRun);
            await eventsContext.SaveChangesAsync();

            ContextProvider.SetJobRunId(jobRunId);
            using var __ = LogContext.PushProperty(LogProperties.JobRunId, jobRunId.ToString());

            await BroadcastJobStatus(hubContext, jobManagementService, jobType, false);

            var handler = scope.ServiceProvider.GetRequiredService<T>();
            await handler.ExecuteAsync();

            status = JobRunStatus.Completed;
            await BroadcastJobStatus(hubContext, jobManagementService, jobType, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{name} failed", typeof(T).Name);
            status = JobRunStatus.Failed;
        }
        finally
        {
            await using var finalScope = _scopeFactory.CreateAsyncScope();
            var eventsContext = finalScope.ServiceProvider.GetRequiredService<EventsContext>();
            var jobRun = await eventsContext.JobRuns.FindAsync(jobRunId);
            if (jobRun is not null)
            {
                jobRun.CompletedAt = DateTime.UtcNow;
                jobRun.Status = status;
                await eventsContext.SaveChangesAsync();
            }
        }
    }

    private async Task BroadcastJobStatus(IHubContext<AppHub> hubContext, IJobManagementService jobManagementService, JobType jobType, bool isFinished)
    {
        try
        {
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