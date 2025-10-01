using System.Collections.Concurrent;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Infrastructure.Utilities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Spi;

namespace Cleanuparr.Infrastructure.Services;

public class JobManagementService : IJobManagementService
{
    private readonly ILogger<JobManagementService> _logger;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IHubContext<Hubs.AppHub> _hubContext;
    private readonly ConcurrentDictionary<string, JobKey> _jobKeys = new();

    public JobManagementService(ILogger<JobManagementService> logger, ISchedulerFactory schedulerFactory, IHubContext<Hubs.AppHub> hubContext)
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
        _hubContext = hubContext;
    }

    public async Task<bool> StartJob(JobType jobType, JobSchedule? schedule = null, string? directCronExpression = null)
    {
        string jobName = jobType.ToString();
        string? cronExpression = null;
        
        // Validate and set the cron expression
        if (directCronExpression != null)
        {
            // Validate direct cron expression
            if (!CronExpressionConverter.IsValidCronExpression(directCronExpression))
            {
                _logger.LogError("Invalid cron expression: {cronExpression}", directCronExpression);
                return false;
            }
            cronExpression = directCronExpression;
        }
        else if (schedule != null)
        {
            try
            {
                // Validate schedule and get cron expression
                schedule.Validate();
                cronExpression = schedule.ToCronExpression();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid job schedule");
                return false;
            }
        }
        
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = new JobKey(jobName);
            
            // Check if job exists, create it if it doesn't
            if (!await scheduler.CheckExists(jobKey))
            {
                _logger.LogError("Job {name} does not exist in scheduler. " +
                                "Jobs should be created at startup by BackgroundJobManager.", jobName);
                return false;
            }

            // Store the job key for later use
            _jobKeys.TryAdd(jobName, jobKey);

            // Clean up all existing triggers for this job first
            await CleanupAllTriggersForJob(scheduler, jobKey);

            // If cron expression is provided, create and schedule the main trigger
            if (!string.IsNullOrEmpty(cronExpression))
            {
                var triggerKey = new TriggerKey($"{jobName}-trigger");
                var newTrigger = TriggerBuilder.Create()
                    .WithIdentity(triggerKey)
                    .ForJob(jobKey)
                    .WithCronSchedule(cronExpression, x => x.WithMisfireHandlingInstructionDoNothing())
                    .Build();
                
                await scheduler.ScheduleJob(newTrigger);
                
                // Compute next fire time for logging
                IReadOnlyList<DateTimeOffset> nextFireTimes = TriggerUtils.ComputeFireTimes((IOperableTrigger)newTrigger, null, 1);
                _logger.LogInformation("Job {name} scheduled with cron expression '{cronExpression}', next run at {nextRunTime}", 
                    jobName, cronExpression, nextFireTimes.FirstOrDefault().LocalDateTime);
                
                // Optionally trigger immediate execution for startup
                // await TriggerJobImmediately(scheduler, jobKey, "startup");
            }
            else
            {
                // If no cron expression, create a one-time trigger to run now
                var oneTimeTrigger = TriggerBuilder.Create()
                    .WithIdentity($"{jobName}-onetime-trigger")
                    .ForJob(jobKey)
                    .StartNow()
                    .Build();
                
                await scheduler.ScheduleJob(oneTimeTrigger);
                _logger.LogInformation("Job {name} scheduled for immediate one-time execution", jobName);
            }

            // Resume the job if it's paused
            await scheduler.ResumeJob(jobKey);
            _logger.LogInformation("Job {name} started successfully", jobName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting job {jobName}", jobName);
            return false;
        }
    }

    /// <summary>
    /// Cleans up all existing triggers for a job to ensure a clean state
    /// </summary>
    private async Task CleanupAllTriggersForJob(IScheduler scheduler, JobKey jobKey)
    {
        try
        {
            var existingTriggers = await scheduler.GetTriggersOfJob(jobKey);
            foreach (var trigger in existingTriggers)
            {
                await scheduler.UnscheduleJob(trigger.Key);
                _logger.LogDebug("Removed existing trigger {triggerKey} for job {jobKey}", 
                    trigger.Key.Name, jobKey.Name);
            }
            
            if (existingTriggers.Any())
            {
                _logger.LogDebug("Cleaned up {count} existing triggers for job {jobName}", 
                    existingTriggers.Count, jobKey.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up triggers for job {jobName}", jobKey.Name);
        }
    }

    /// <summary>
    /// Triggers a job immediately with a one-time trigger
    /// </summary>
    private async Task TriggerJobImmediately(IScheduler scheduler, JobKey jobKey, string reason)
    {
        try
        {
            var immediateTrigger = TriggerBuilder.Create()
                .WithIdentity($"{jobKey.Name}-immediate-{reason}-{DateTimeOffset.UtcNow.Ticks}")
                .ForJob(jobKey)
                .StartNow()
                .Build();
            
            await scheduler.ScheduleJob(immediateTrigger);
            _logger.LogDebug("Triggered job {jobName} immediately for reason: {reason}", jobKey.Name, reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trigger job {jobName} immediately", jobKey.Name);
        }
    }

    /// <summary>
    /// Gets the main scheduled trigger for a job (excludes one-time triggers)
    /// </summary>
    public async Task<ITrigger?> GetMainTrigger(JobType jobType)
    {
        string jobName = jobType.ToString();
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = new JobKey(jobName);
            
            if (!await scheduler.CheckExists(jobKey))
            {
                return null;
            }

            // Look for the main trigger (follows our naming convention)
            var mainTriggerKey = new TriggerKey($"{jobName}-trigger");
            return await scheduler.GetTrigger(mainTriggerKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting main trigger for job {jobName}", jobName);
            return null;
        }
    }

    
    public async Task<bool> StopJob(JobType jobType)
    {
        string jobName = jobType.ToString();
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = new JobKey(jobName);
            
            if (!await scheduler.CheckExists(jobKey))
            {
                _logger.LogError("Job {name} does not exist", jobName);
                return false;
            }

            // Clean up all triggers for this job (reuse our centralized method)
            await CleanupAllTriggersForJob(scheduler, jobKey);

            _logger.LogInformation("Job {name} stopped successfully", jobName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping job {jobName}", jobName);
            return false;
        }
    }

    public async Task<IReadOnlyList<JobInfo>> GetAllJobs(IScheduler? scheduler = null)
    {
        try
        {
            scheduler ??= await _schedulerFactory.GetScheduler();
            var result = new List<JobInfo>();
            
            var jobGroups = await scheduler.GetJobGroupNames();
            foreach (var group in jobGroups)
            {
                var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(group));
                foreach (var jobKey in jobKeys)
                {
                    var jobDetail = await scheduler.GetJobDetail(jobKey);
                    var triggers = await scheduler.GetTriggersOfJob(jobKey);
                    var jobInfo = new JobInfo
                    {
                        Name = jobKey.Name,
                        JobType = jobKey.Name, // Use the job key name instead of generic type
                        Status = "Not Scheduled"
                    };
                    
                    if (triggers.Any())
                    {
                        var trigger = triggers.First();
                        var triggerState = await scheduler.GetTriggerState(trigger.Key);
                        
                        jobInfo.Status = triggerState switch
                        {
                            TriggerState.Normal => "Scheduled",
                            TriggerState.Paused => "Paused",
                            TriggerState.Complete => "Complete",
                            TriggerState.Error => "Error",
                            TriggerState.Blocked => "Running",
                            TriggerState.None => "Not Scheduled",
                            _ => "Unknown"
                        };
                        
                        if (trigger is ICronTrigger cronTrigger)
                        {
                            jobInfo.Schedule = cronTrigger.CronExpressionString;
                        }
                        
                        jobInfo.NextRunTime = trigger.GetNextFireTimeUtc()?.UtcDateTime;
                        jobInfo.PreviousRunTime = trigger.GetPreviousFireTimeUtc()?.UtcDateTime;
                    }
                    
                    result.Add(jobInfo);
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all jobs");
            return new List<JobInfo>();
        }
    }

    public async Task<JobInfo> GetJob(JobType jobType)
    {
        string jobName = jobType.ToString();
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = new JobKey(jobName);
            
            if (!await scheduler.CheckExists(jobKey))
            {
                _logger.LogError("Job {name} does not exist", jobName);
                return new JobInfo { Name = jobName, Status = "Not Found" };
            }

            var jobDetail = await scheduler.GetJobDetail(jobKey);
            var triggers = await scheduler.GetTriggersOfJob(jobKey);
            
            var jobInfo = new JobInfo
            {
                Name = jobName,
                JobType = jobName, // Use the job key name instead of generic type
                Status = "Not Scheduled"
            };
            
            if (triggers.Any())
            {
                var trigger = triggers.First();
                var state = await scheduler.GetTriggerState(trigger.Key);
                
                jobInfo.Status = state switch
                {
                    TriggerState.Normal => "Scheduled", // Normal means trigger is scheduled and ready to fire
                    TriggerState.Paused => "Paused",
                    TriggerState.Complete => "Complete",
                    TriggerState.Error => "Error",
                    TriggerState.Blocked => "Running", // Blocked typically means job is currently executing
                    TriggerState.None => "Not Scheduled",
                    _ => "Unknown"
                };
                
                if (trigger is ICronTrigger cronTrigger)
                {
                    jobInfo.Schedule = cronTrigger.CronExpressionString;
                }
                
                jobInfo.NextRunTime = trigger.GetNextFireTimeUtc()?.LocalDateTime;
                jobInfo.PreviousRunTime = trigger.GetPreviousFireTimeUtc()?.LocalDateTime;
            }
            
            return jobInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job {name}", jobName);
            return new JobInfo { Name = jobName, Status = "Error" };
        }
    }

    public async Task<bool> TriggerJobOnce(JobType jobType)
    {
        string jobName = jobType.ToString();
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = new JobKey(jobName);
            
            if (!await scheduler.CheckExists(jobKey))
            {
                _logger.LogError("Job {name} does not exist", jobName);
                return false;
            }

            await TriggerJobImmediately(scheduler, jobKey, "manual");
            _logger.LogInformation("Job {name} triggered for one-time execution", jobName);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering job {jobName}", jobName);
            return false;
        }
    }

    public async Task<bool> UpdateJobSchedule(JobType jobType, JobSchedule schedule)
    {
        if (schedule == null)
            throw new ArgumentNullException(nameof(schedule));
            
        string jobName = jobType.ToString();
        string cronExpression = schedule.ToCronExpression();
        
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = new JobKey(jobName);
            
            if (!await scheduler.CheckExists(jobKey))
            {
                _logger.LogError("Job {name} does not exist", jobName);
                return false;
            }

            // Clean up all existing triggers for this job
            await CleanupAllTriggersForJob(scheduler, jobKey);

            // Create new trigger with consistent naming
            var triggerKey = new TriggerKey($"{jobName}-trigger");
            var newTrigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .ForJob(jobKey)
                .WithCronSchedule(cronExpression, x => x.WithMisfireHandlingInstructionDoNothing())
                .Build();
            
            await scheduler.ScheduleJob(newTrigger);
            
            _logger.LogInformation("Job {name} schedule updated successfully to {cronExpression}", jobName, cronExpression);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job {name} schedule", jobName);
            return false;
        }
    }
}
