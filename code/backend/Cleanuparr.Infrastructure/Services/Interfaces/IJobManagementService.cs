using Cleanuparr.Infrastructure.Models;
using Quartz;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IJobManagementService
{
    Task<bool> StartJob(JobType jobType, JobSchedule? schedule = null, string? directCronExpression = null);
    Task<bool> StopJob(JobType jobType);
    Task<bool> TriggerJobOnce(JobType jobType);
    Task<IReadOnlyList<JobInfo>> GetAllJobs(IScheduler? scheduler = null);
    Task<JobInfo> GetJob(JobType jobType);
    Task<bool> UpdateJobSchedule(JobType jobType, JobSchedule schedule);
    Task<ITrigger?> GetMainTrigger(JobType jobType);
}