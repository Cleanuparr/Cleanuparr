using Cleanuparr.Application.Features.ContentBlocker;
using Cleanuparr.Application.Features.DownloadCleaner;
using Cleanuparr.Application.Features.QueueCleaner;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.ContentBlocker;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Spi;

namespace Cleanuparr.Api.Jobs;

/// <summary>
/// Manages background jobs in the application.
/// This class is responsible for reading configurations and scheduling jobs.
/// </summary>
public class BackgroundJobManager : IHostedService
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundJobManager> _logger;
    private IScheduler? _scheduler;

    public BackgroundJobManager(
        ISchedulerFactory schedulerFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundJobManager> logger
    )
    {
        _schedulerFactory = schedulerFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Starts the background job manager.
    /// This method is called when the application starts.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting BackgroundJobManager");
            _scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        
            await InitializeJobsFromConfiguration(cancellationToken);
        
            _logger.LogInformation("BackgroundJobManager started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start BackgroundJobManager");
        }
    }

    /// <summary>
    /// Stops the background job manager.
    /// This method is called when the application stops.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping BackgroundJobManager");
        
        if (_scheduler != null)
        {
            // Don't shutdown the scheduler as it's managed by QuartzHostedService
            await _scheduler.Standby(cancellationToken);
        }
        
        _logger.LogInformation("BackgroundJobManager stopped");
    }
    
    /// <summary>
    /// Initializes jobs based on current configuration settings.
    /// Always registers jobs in the scheduler, but only adds triggers for enabled jobs.
    /// </summary>
    private async Task InitializeJobsFromConfiguration(CancellationToken cancellationToken = default)
    {
        if (_scheduler == null)
        {
            throw new InvalidOperationException("Scheduler not initialized");
        }
        
        // Use scoped DataContext to prevent memory leaks
        await using var scope = _scopeFactory.CreateAsyncScope();
        await using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        
        // Get configurations from db
        QueueCleanerConfig queueCleanerConfig = await dataContext.QueueCleanerConfigs
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        ContentBlockerConfig contentBlockerConfig = await dataContext.ContentBlockerConfigs
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        DownloadCleanerConfig downloadCleanerConfig = await dataContext.DownloadCleanerConfigs
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        
        // Always register jobs, regardless of enabled status
        await RegisterQueueCleanerJob(queueCleanerConfig, cancellationToken);
        await RegisterContentBlockerJob(contentBlockerConfig, cancellationToken);
        await RegisterDownloadCleanerJob(downloadCleanerConfig, cancellationToken);
    }
    
    /// <summary>
    /// Registers the QueueCleaner job and optionally adds triggers based on configuration.
    /// </summary>
    public async Task RegisterQueueCleanerJob(
        QueueCleanerConfig config, 
        CancellationToken cancellationToken = default)
    {
        // Always register the job definition
        await AddJobWithoutTrigger<QueueCleaner>(cancellationToken);
        
        // Only add triggers if the job is enabled
        if (config.Enabled)
        {
            await AddTriggersForJob<QueueCleaner>(config, config.CronExpression, cancellationToken);
        }
    }
    
    /// <summary>
    /// Registers the QueueCleaner job and optionally adds triggers based on configuration.
    /// </summary>
    public async Task RegisterContentBlockerJob(
        ContentBlockerConfig config, 
        CancellationToken cancellationToken = default)
    {
        // Always register the job definition
        await AddJobWithoutTrigger<ContentBlocker>(cancellationToken);
        
        // Only add triggers if the job is enabled
        if (config.Enabled)
        {
            await AddTriggersForJob<ContentBlocker>(config, config.CronExpression, cancellationToken);
        }
    }
    
    /// <summary>
    /// Registers the DownloadCleaner job and optionally adds triggers based on configuration.
    /// </summary>
    public async Task RegisterDownloadCleanerJob(DownloadCleanerConfig config, CancellationToken cancellationToken = default)
    {
        // Always register the job definition
        await AddJobWithoutTrigger<DownloadCleaner>(cancellationToken);
        
        // Only add triggers if the job is enabled
        if (config.Enabled)
        {
            await AddTriggersForJob<DownloadCleaner>(config, config.CronExpression, cancellationToken);
        }
    }
    
    /// <summary>
    /// Helper method to add triggers for an existing job.
    /// </summary>
    private async Task AddTriggersForJob<T>(
        IJobConfig config,
        string cronExpression,
        CancellationToken cancellationToken = default) 
        where T : GenericHandler
    {
        if (_scheduler == null)
        {
            throw new InvalidOperationException("Scheduler not initialized");
        }
        
        string typeName = typeof(T).Name;
        var jobKey = new JobKey(typeName);
        
        // Validate the cron expression
        if (!string.IsNullOrEmpty(cronExpression))
        {
            IOperableTrigger triggerObj = (IOperableTrigger)TriggerBuilder.Create()
                .WithIdentity("ValidationTrigger")
                .StartNow()
                .WithCronSchedule(cronExpression, x => x.WithMisfireHandlingInstructionDoNothing())
                .Build();

            IReadOnlyList<DateTimeOffset> nextFireTimes = TriggerUtils.ComputeFireTimes(triggerObj, null, 2);
            TimeSpan triggerValue = nextFireTimes[1] - nextFireTimes[0];
            
            if (triggerValue > Constants.TriggerMaxLimit)
            {
                throw new ValidationException($"{cronExpression} should have a fire time of maximum {Constants.TriggerMaxLimit.TotalHours} hours");
            }
            
            if (typeof(T) != typeof(ContentBlocker) && triggerValue < Constants.TriggerMinLimit)
            {
                throw new ValidationException($"{cronExpression} should have a fire time of minimum {Constants.TriggerMinLimit.TotalSeconds} seconds");
            }

            if (triggerValue > StaticConfiguration.TriggerValue)
            {
                StaticConfiguration.TriggerValue = triggerValue;
            }
        }
        
        // Create main cron trigger with consistent naming (matches JobManagementService)
        var trigger = TriggerBuilder.Create()
            .WithIdentity($"{typeName}-trigger")
            .ForJob(jobKey)
            .WithCronSchedule(cronExpression, x => x.WithMisfireHandlingInstructionDoNothing())
            .Build();
        
        // Schedule the main trigger
        await _scheduler.ScheduleJob(trigger, cancellationToken);
        
        // Trigger immediate execution for startup using a one-time trigger
        var startupTrigger = TriggerBuilder.Create()
            .WithIdentity($"{typeName}-startup-{DateTimeOffset.UtcNow.Ticks}")
            .ForJob(jobKey)
            .StartNow()
            .Build();
        
        await _scheduler.ScheduleJob(startupTrigger, cancellationToken);
        
        _logger.LogInformation("Added trigger for job {name} with cron expression {CronExpression} and immediate startup execution", 
            typeName, cronExpression);
    }
    
    /// <summary>
    /// Helper method to add a job without a trigger (for chained jobs).
    /// </summary>
    private async Task AddJobWithoutTrigger<T>(CancellationToken cancellationToken = default) 
        where T : GenericHandler
    {
        if (_scheduler == null)
        {
            throw new InvalidOperationException("Scheduler not initialized");
        }
        
        string typeName = typeof(T).Name;
        var jobKey = new JobKey(typeName);
        
        // Check if job already exists
        if (await _scheduler.CheckExists(jobKey, cancellationToken))
        {
            _logger.LogDebug("Job {name} already exists, skipping registration", typeName);
            return;
        }
        
        // Create job detail that is durable (can exist without triggers)
        var jobDetail = JobBuilder.Create<GenericJob<T>>()
            .WithIdentity(jobKey)
            .StoreDurably()
            .Build();
        
        // Add job to scheduler
        await _scheduler.AddJob(jobDetail, true, cancellationToken);
        
        _logger.LogInformation("Registered job {name} without trigger", typeName);
    }
}
