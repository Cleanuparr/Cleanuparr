using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.BlacklistSync;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Cleanuparr.Persistence.Models.Configuration.BlacklistSync;
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
            _logger.LogDebug("Starting BackgroundJobManager");
            _scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        
            await InitializeJobsFromConfiguration(cancellationToken);
        
            _logger.LogDebug("BackgroundJobManager started");
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
        _logger.LogDebug("Stopping BackgroundJobManager");
        
        if (_scheduler != null)
        {
            // Don't shut down the scheduler as it's managed by QuartzHostedService
            await _scheduler.Standby(cancellationToken);
        }
        
        _logger.LogDebug("BackgroundJobManager stopped");
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
        
        await using var scope = _scopeFactory.CreateAsyncScope();
        await using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        
        // Get configurations from db
        QueueCleanerConfig queueCleanerConfig = await dataContext.QueueCleanerConfigs
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        ContentBlockerConfig malwareBlockerConfig = await dataContext.ContentBlockerConfigs
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        DownloadCleanerConfig downloadCleanerConfig = await dataContext.DownloadCleanerConfigs
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        BlacklistSyncConfig blacklistSyncConfig = await dataContext.BlacklistSyncConfigs
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        
        // Always register jobs, regardless of enabled status
        await RegisterQueueCleanerJob(queueCleanerConfig, cancellationToken);
        await RegisterMalwareBlockerJob(malwareBlockerConfig, cancellationToken);
        await RegisterDownloadCleanerJob(downloadCleanerConfig, cancellationToken);
        await RegisterBlacklistSyncJob(blacklistSyncConfig, cancellationToken);
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
            await AddTriggersForJob<QueueCleaner>(config.CronExpression, cancellationToken);
        }
    }
    
    /// <summary>
    /// Registers the QueueCleaner job and optionally adds triggers based on configuration.
    /// </summary>
    public async Task RegisterMalwareBlockerJob(
        ContentBlockerConfig config, 
        CancellationToken cancellationToken = default)
    {
        // Always register the job definition
        await AddJobWithoutTrigger<MalwareBlocker>(cancellationToken);
        
        // Only add triggers if the job is enabled
        if (config.Enabled)
        {
            await AddTriggersForJob<MalwareBlocker>(config.CronExpression, cancellationToken);
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
            await AddTriggersForJob<DownloadCleaner>(config.CronExpression, cancellationToken);
        }
    }

    /// <summary>
    /// Registers the BlacklistSync job and optionally adds triggers based on general configuration.
    /// </summary>
    public async Task RegisterBlacklistSyncJob(BlacklistSyncConfig config, CancellationToken cancellationToken = default)
    {
        // Always register the job definition
        await AddJobWithoutTrigger<BlacklistSynchronizer>(cancellationToken);

        if (config.Enabled)
        {
            await AddTriggersForJob<BlacklistSynchronizer>(config.CronExpression, cancellationToken);
        }
    }
    
    /// <summary>
    /// Helper method to add triggers for an existing job.
    /// </summary>
    private async Task AddTriggersForJob<T>(
        string cronExpression,
        CancellationToken cancellationToken = default) 
        where T : IHandler
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
            
            if (typeof(T) != typeof(MalwareBlocker) && triggerValue < Constants.TriggerMinLimit)
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
        
        _logger.LogInformation("Added trigger for job {name} with cron expression {CronExpression}", 
            typeName, cronExpression);
    }
    
    /// <summary>
    /// Helper method to add a job without a trigger (for chained jobs).
    /// </summary>
    private async Task AddJobWithoutTrigger<T>(CancellationToken cancellationToken = default) 
        where T : IHandler
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
        
        _logger.LogDebug("Registered job {name} without trigger", typeName);
    }
}
