using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using Quartz.Impl.Matchers;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class JobManagementServiceTests
{
    private readonly ILogger<JobManagementService> _logger;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IScheduler _scheduler;
    private readonly IHubContext<AppHub> _hubContext;
    private readonly JobManagementService _service;

    public JobManagementServiceTests()
    {
        _logger = Substitute.For<ILogger<JobManagementService>>();
        _schedulerFactory = Substitute.For<ISchedulerFactory>();
        _scheduler = Substitute.For<IScheduler>();
        _hubContext = Substitute.For<IHubContext<AppHub>>();

        _schedulerFactory.GetScheduler(Arg.Any<CancellationToken>())
            .Returns(_scheduler);

        _service = new JobManagementService(_logger, _schedulerFactory, _hubContext);
    }

    #region StartJob Tests

    [Fact]
    public async Task StartJob_WithInvalidDirectCronExpression_ReturnsFalse()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;
        var invalidCron = "invalid-cron";

        // Act
        var result = await _service.StartJob(jobType, directCronExpression: invalidCron);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StartJob_JobDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;
        var cronExpression = "0 0/5 * * * ?"; // Every 5 minutes

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _service.StartJob(jobType, directCronExpression: cronExpression);

        // Assert
        Assert.False(result);
        _logger.ReceivedLogContaining(LogLevel.Error, "does not exist");
    }

    [Fact]
    public async Task StartJob_WithValidCronExpression_ReturnsTrue()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;
        var cronExpression = "0 0/5 * * * ?"; // Every 5 minutes

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _scheduler.GetTriggersOfJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(new List<ITrigger>());
        _scheduler.ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<CancellationToken>())
            .Returns(DateTimeOffset.Now);

        // Act
        var result = await _service.StartJob(jobType, directCronExpression: cronExpression);

        // Assert
        Assert.True(result);
        await _scheduler.Received(1).ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
        await _scheduler.Received(1).ResumeJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartJob_WithSchedule_ReturnsTrue()
    {
        // Arrange
        var jobType = JobType.MalwareBlocker;
        var schedule = new JobSchedule { Every = 5, Type = ScheduleUnit.Minutes };

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _scheduler.GetTriggersOfJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(new List<ITrigger>());
        _scheduler.ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<CancellationToken>())
            .Returns(DateTimeOffset.Now);

        // Act
        var result = await _service.StartJob(jobType, schedule: schedule);

        // Assert
        Assert.True(result);
        await _scheduler.Received(1).ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartJob_WithNoScheduleOrCron_CreatesOneTimeTrigger()
    {
        // Arrange
        var jobType = JobType.DownloadCleaner;

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _scheduler.GetTriggersOfJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(new List<ITrigger>());
        _scheduler.ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<CancellationToken>())
            .Returns(DateTimeOffset.Now);

        // Act
        var result = await _service.StartJob(jobType);

        // Assert
        Assert.True(result);
        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<ITrigger>(t => t.Key.Name.Contains("onetime")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartJob_CleansUpExistingTriggers_BeforeSchedulingNew()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;
        var cronExpression = "0 0/5 * * * ?";

        var existingTrigger = Substitute.For<ITrigger>();
        existingTrigger.Key.Returns(new TriggerKey("existing-trigger"));

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _scheduler.GetTriggersOfJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(new List<ITrigger> { existingTrigger });
        _scheduler.ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<CancellationToken>())
            .Returns(DateTimeOffset.Now);

        // Act
        var result = await _service.StartJob(jobType, directCronExpression: cronExpression);

        // Assert
        Assert.True(result);
        await _scheduler.Received(1).UnscheduleJob(
            Arg.Is<TriggerKey>(k => k.Name == "existing-trigger"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartJob_WhenSchedulerThrows_ReturnsFalse()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;
        var cronExpression = "0 0/5 * * * ?";

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Scheduler error"));

        // Act
        var result = await _service.StartJob(jobType, directCronExpression: cronExpression);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region StopJob Tests

    [Fact]
    public async Task StopJob_JobDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _service.StopJob(jobType);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StopJob_JobExists_CleansUpTriggersAndReturnsTrue()
    {
        // Arrange
        var jobType = JobType.MalwareBlocker;

        var trigger = Substitute.For<ITrigger>();
        trigger.Key.Returns(new TriggerKey("test-trigger"));

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _scheduler.GetTriggersOfJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(new List<ITrigger> { trigger });

        // Act
        var result = await _service.StopJob(jobType);

        // Assert
        Assert.True(result);
        await _scheduler.Received(1).UnscheduleJob(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopJob_WhenSchedulerThrows_ReturnsFalse()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Scheduler error"));

        // Act
        var result = await _service.StopJob(jobType);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region GetJob Tests

    [Fact]
    public async Task GetJob_JobDoesNotExist_ReturnsNotFoundStatus()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _service.GetJob(jobType);

        // Assert
        Assert.Equal("Not Found", result.Status);
        Assert.Equal("QueueCleaner", result.Name);
    }

    [Fact]
    public async Task GetJob_JobExistsNoTriggers_ReturnsNotScheduledStatus()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _scheduler.GetTriggersOfJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(new List<ITrigger>());

        // Act
        var result = await _service.GetJob(jobType);

        // Assert
        Assert.Equal("Not Scheduled", result.Status);
    }

    [Theory]
    [InlineData(TriggerState.Normal, "Scheduled")]
    [InlineData(TriggerState.Paused, "Paused")]
    [InlineData(TriggerState.Complete, "Complete")]
    [InlineData(TriggerState.Error, "Error")]
    [InlineData(TriggerState.Blocked, "Running")]
    [InlineData(TriggerState.None, "Not Scheduled")]
    public async Task GetJob_WithTrigger_ReturnsCorrectStatus(TriggerState triggerState, string expectedStatus)
    {
        // Arrange
        var jobType = JobType.QueueCleaner;

        var trigger = Substitute.For<ITrigger>();
        trigger.Key.Returns(new TriggerKey("test-trigger"));
        trigger.GetNextFireTimeUtc().Returns(DateTimeOffset.UtcNow.AddMinutes(5));
        trigger.GetPreviousFireTimeUtc().Returns(DateTimeOffset.UtcNow.AddMinutes(-5));

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _scheduler.GetTriggersOfJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(new List<ITrigger> { trigger });
        _scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>())
            .Returns(triggerState);

        // Act
        var result = await _service.GetJob(jobType);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
    }

    [Fact]
    public async Task GetJob_WhenSchedulerThrows_ReturnsErrorStatus()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Scheduler error"));

        // Act
        var result = await _service.GetJob(jobType);

        // Assert
        Assert.Equal("Error", result.Status);
    }

    #endregion

    #region GetAllJobs Tests

    [Fact]
    public async Task GetAllJobs_NoJobs_ReturnsEmptyList()
    {
        // Arrange
        _scheduler.GetJobGroupNames(Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        // Act
        var result = await _service.GetAllJobs();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllJobs_WithJobs_ReturnsJobList()
    {
        // Arrange
        var jobKey = new JobKey("QueueCleaner");
        var trigger = Substitute.For<ITrigger>();
        trigger.Key.Returns(new TriggerKey("test-trigger"));
        trigger.GetNextFireTimeUtc().Returns(DateTimeOffset.UtcNow.AddMinutes(5));

        _scheduler.GetJobGroupNames(Arg.Any<CancellationToken>())
            .Returns(new List<string> { "DEFAULT" });
        _scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey> { jobKey });
        _scheduler.GetTriggersOfJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(new List<ITrigger> { trigger });
        _scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>())
            .Returns(TriggerState.Normal);

        // Act
        var result = await _service.GetAllJobs();

        // Assert
        Assert.Single(result);
        Assert.Equal("QueueCleaner", result[0].Name);
        Assert.Equal("Scheduled", result[0].Status);
    }

    [Fact]
    public async Task GetAllJobs_WhenSchedulerThrows_ReturnsEmptyList()
    {
        // Arrange
        _scheduler.GetJobGroupNames(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Scheduler error"));

        // Act
        var result = await _service.GetAllJobs();

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region TriggerJobOnce Tests

    [Fact]
    public async Task TriggerJobOnce_JobDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _service.TriggerJobOnce(jobType);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TriggerJobOnce_JobExists_TriggersJobAndReturnsTrue()
    {
        // Arrange
        var jobType = JobType.MalwareBlocker;

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _scheduler.ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<CancellationToken>())
            .Returns(DateTimeOffset.Now);

        // Act
        var result = await _service.TriggerJobOnce(jobType);

        // Assert
        Assert.True(result);
        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<ITrigger>(t => t.Key.Name.Contains("immediate") && t.Key.Name.Contains("manual")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerJobOnce_WhenSchedulerThrows_ReturnsFalse()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Scheduler error"));

        // Act
        var result = await _service.TriggerJobOnce(jobType);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region UpdateJobSchedule Tests

    [Fact]
    public async Task UpdateJobSchedule_NullSchedule_ThrowsArgumentNullException()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.UpdateJobSchedule(jobType, null!));
    }

    [Fact]
    public async Task UpdateJobSchedule_JobDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;
        var schedule = new JobSchedule { Every = 5, Type = ScheduleUnit.Minutes };

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _service.UpdateJobSchedule(jobType, schedule);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateJobSchedule_ValidSchedule_ReturnsTrue()
    {
        // Arrange
        var jobType = JobType.DownloadCleaner;
        var schedule = new JobSchedule { Every = 10, Type = ScheduleUnit.Minutes };

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _scheduler.GetTriggersOfJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(new List<ITrigger>());
        _scheduler.ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<CancellationToken>())
            .Returns(DateTimeOffset.Now);

        // Act
        var result = await _service.UpdateJobSchedule(jobType, schedule);

        // Assert
        Assert.True(result);
        await _scheduler.Received(1).ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateJobSchedule_WhenSchedulerThrows_ReturnsFalse()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;
        var schedule = new JobSchedule { Every = 5, Type = ScheduleUnit.Minutes };

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Scheduler error"));

        // Act
        var result = await _service.UpdateJobSchedule(jobType, schedule);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region GetMainTrigger Tests

    [Fact]
    public async Task GetMainTrigger_JobDoesNotExist_ReturnsNull()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _service.GetMainTrigger(jobType);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMainTrigger_TriggerExists_ReturnsTrigger()
    {
        // Arrange
        var jobType = JobType.MalwareBlocker;
        var expectedTriggerKey = new TriggerKey("MalwareBlocker-trigger");

        var trigger = Substitute.For<ITrigger>();
        trigger.Key.Returns(expectedTriggerKey);

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _scheduler.GetTrigger(expectedTriggerKey, Arg.Any<CancellationToken>())
            .Returns(trigger);

        // Act
        var result = await _service.GetMainTrigger(jobType);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedTriggerKey, result.Key);
    }

    [Fact]
    public async Task GetMainTrigger_WhenSchedulerThrows_ReturnsNull()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;

        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Scheduler error"));

        // Act
        var result = await _service.GetMainTrigger(jobType);

        // Assert
        Assert.Null(result);
    }

    #endregion
}
