using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Models;
using Quartz;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IJobManagementService
{
    Task<bool> StartJob(JobType jobType, JobSchedule? schedule = null, string? directCronExpression = null);
    Task<bool> StopJob(JobType jobType);
    Task<bool> TriggerJobOnce(JobType jobType);

    /// <summary>
    /// Schedules the first targeted MalwareBlocker scan for a single download received via an *arr "On Grab" webhook.
    /// Subsequent retries are scheduled by the handler via <see cref="ScheduleMalwareBlockerWebhookRetry"/> only while the download has not been found.
    /// </summary>
    Task<bool> TriggerMalwareBlockerWebhook(Guid instanceId, string downloadId, long contentId, InstanceType type);

    /// <summary>
    /// Schedules the next targeted MalwareBlocker webhook scan after a completed attempt.
    /// </summary>
    Task<bool> ScheduleMalwareBlockerWebhookRetry(WebhookScanTarget target);
    
    Task<IReadOnlyList<JobInfo>> GetAllJobs(IScheduler? scheduler = null);
    Task<JobInfo> GetJob(JobType jobType);
    Task<bool> UpdateJobSchedule(JobType jobType, JobSchedule schedule);
    Task<ITrigger?> GetMainTrigger(JobType jobType);
}