using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Models;
using Quartz;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IJobManagementService
{
    Task<bool> StartJob(JobType jobType, JobSchedule? schedule = null, string? directCronExpression = null);
    Task<bool> StopJob(JobType jobType);
    Task<bool> TriggerJobOnce(JobType jobType);

    /// <summary>
    /// Schedules targeted MalwareBlocker scans for a single download received via an *arr "On Grab"
    /// webhook. Runs immediately plus a few delayed retries (to catch late torrent metadata) on the
    /// dedicated webhook JobKey, independent of the scheduled MalwareBlocker job.
    /// </summary>
    Task<bool> TriggerMalwareBlockerWebhook(Guid instanceId, string downloadId, long contentId, InstanceType type);
    Task<IReadOnlyList<JobInfo>> GetAllJobs(IScheduler? scheduler = null);
    Task<JobInfo> GetJob(JobType jobType);
    Task<bool> UpdateJobSchedule(JobType jobType, JobSchedule schedule);
    Task<ITrigger?> GetMainTrigger(JobType jobType);
}