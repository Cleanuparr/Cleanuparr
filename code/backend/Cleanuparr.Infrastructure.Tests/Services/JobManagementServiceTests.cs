using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Quartz.Impl.Matchers;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class JobManagementServiceTests
{
    private readonly Mock<ILogger<JobManagementService>> _loggerMock;
    private readonly Mock<ISchedulerFactory> _schedulerFactoryMock;
    private readonly Mock<IScheduler> _schedulerMock;
    private readonly Mock<IHubContext<AppHub>> _hubContextMock;
    private readonly JobManagementService _service;

    public JobManagementServiceTests()
    {
        _loggerMock = new Mock<ILogger<JobManagementService>>();
        _schedulerFactoryMock = new Mock<ISchedulerFactory>();
        _schedulerMock = new Mock<IScheduler>();
        _hubContextMock = new Mock<IHubContext<AppHub>>();

        _schedulerFactoryMock.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_schedulerMock.Object);

        _service = new JobManagementService(_loggerMock.Object, _schedulerFactoryMock.Object, _hubContextMock.Object);
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

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.StartJob(jobType, directCronExpression: cronExpression);

        // Assert
        Assert.False(result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("does not exist")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartJob_WithValidCronExpression_ReturnsTrue()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;
        var cronExpression = "0 0/5 * * * ?"; // Every 5 minutes

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _schedulerMock.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ITrigger>());
        _schedulerMock.Setup(s => s.ScheduleJob(It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.Now);

        // Act
        var result = await _service.StartJob(jobType, directCronExpression: cronExpression);

        // Assert
        Assert.True(result);
        _schedulerMock.Verify(s => s.ScheduleJob(It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()), Times.Once);
        _schedulerMock.Verify(s => s.ResumeJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartJob_WithSchedule_ReturnsTrue()
    {
        // Arrange
        var jobType = JobType.MalwareBlocker;
        var schedule = new JobSchedule { Every = 5, Type = ScheduleUnit.Minutes };

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _schedulerMock.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ITrigger>());
        _schedulerMock.Setup(s => s.ScheduleJob(It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.Now);

        // Act
        var result = await _service.StartJob(jobType, schedule: schedule);

        // Assert
        Assert.True(result);
        _schedulerMock.Verify(s => s.ScheduleJob(It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartJob_WithNoScheduleOrCron_CreatesOneTimeTrigger()
    {
        // Arrange
        var jobType = JobType.DownloadCleaner;

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _schedulerMock.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ITrigger>());
        _schedulerMock.Setup(s => s.ScheduleJob(It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.Now);

        // Act
        var result = await _service.StartJob(jobType);

        // Assert
        Assert.True(result);
        _schedulerMock.Verify(s => s.ScheduleJob(
            It.Is<ITrigger>(t => t.Key.Name.Contains("onetime")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartJob_CleansUpExistingTriggers_BeforeSchedulingNew()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;
        var cronExpression = "0 0/5 * * * ?";

        var existingTriggerMock = new Mock<ITrigger>();
        existingTriggerMock.Setup(t => t.Key).Returns(new TriggerKey("existing-trigger"));

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _schedulerMock.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ITrigger> { existingTriggerMock.Object });
        _schedulerMock.Setup(s => s.ScheduleJob(It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.Now);

        // Act
        var result = await _service.StartJob(jobType, directCronExpression: cronExpression);

        // Assert
        Assert.True(result);
        _schedulerMock.Verify(s => s.UnscheduleJob(
            It.Is<TriggerKey>(k => k.Name == "existing-trigger"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartJob_WhenSchedulerThrows_ReturnsFalse()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;
        var cronExpression = "0 0/5 * * * ?";

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
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

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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

        var triggerMock = new Mock<ITrigger>();
        triggerMock.Setup(t => t.Key).Returns(new TriggerKey("test-trigger"));

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _schedulerMock.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ITrigger> { triggerMock.Object });

        // Act
        var result = await _service.StopJob(jobType);

        // Assert
        Assert.True(result);
        _schedulerMock.Verify(s => s.UnscheduleJob(It.IsAny<TriggerKey>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopJob_WhenSchedulerThrows_ReturnsFalse()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
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

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _schedulerMock.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ITrigger>());

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

        var triggerMock = new Mock<ITrigger>();
        triggerMock.Setup(t => t.Key).Returns(new TriggerKey("test-trigger"));
        triggerMock.Setup(t => t.GetNextFireTimeUtc()).Returns(DateTimeOffset.UtcNow.AddMinutes(5));
        triggerMock.Setup(t => t.GetPreviousFireTimeUtc()).Returns(DateTimeOffset.UtcNow.AddMinutes(-5));

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _schedulerMock.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ITrigger> { triggerMock.Object });
        _schedulerMock.Setup(s => s.GetTriggerState(It.IsAny<TriggerKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(triggerState);

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

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
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
        _schedulerMock.Setup(s => s.GetJobGroupNames(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

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
        var triggerMock = new Mock<ITrigger>();
        triggerMock.Setup(t => t.Key).Returns(new TriggerKey("test-trigger"));
        triggerMock.Setup(t => t.GetNextFireTimeUtc()).Returns(DateTimeOffset.UtcNow.AddMinutes(5));

        _schedulerMock.Setup(s => s.GetJobGroupNames(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "DEFAULT" });
        _schedulerMock.Setup(s => s.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<JobKey> { jobKey });
        _schedulerMock.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ITrigger> { triggerMock.Object });
        _schedulerMock.Setup(s => s.GetTriggerState(It.IsAny<TriggerKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TriggerState.Normal);

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
        _schedulerMock.Setup(s => s.GetJobGroupNames(It.IsAny<CancellationToken>()))
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

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _schedulerMock.Setup(s => s.ScheduleJob(It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.Now);

        // Act
        var result = await _service.TriggerJobOnce(jobType);

        // Assert
        Assert.True(result);
        _schedulerMock.Verify(s => s.ScheduleJob(
            It.Is<ITrigger>(t => t.Key.Name.Contains("immediate") && t.Key.Name.Contains("manual")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TriggerJobOnce_WhenSchedulerThrows_ReturnsFalse()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
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

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _schedulerMock.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ITrigger>());
        _schedulerMock.Setup(s => s.ScheduleJob(It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.Now);

        // Act
        var result = await _service.UpdateJobSchedule(jobType, schedule);

        // Assert
        Assert.True(result);
        _schedulerMock.Verify(s => s.ScheduleJob(It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateJobSchedule_WhenSchedulerThrows_ReturnsFalse()
    {
        // Arrange
        var jobType = JobType.QueueCleaner;
        var schedule = new JobSchedule { Every = 5, Type = ScheduleUnit.Minutes };

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
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

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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

        var triggerMock = new Mock<ITrigger>();
        triggerMock.Setup(t => t.Key).Returns(expectedTriggerKey);

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _schedulerMock.Setup(s => s.GetTrigger(expectedTriggerKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(triggerMock.Object);

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

        _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Scheduler error"));

        // Act
        var result = await _service.GetMainTrigger(jobType);

        // Assert
        Assert.Null(result);
    }

    #endregion
}
