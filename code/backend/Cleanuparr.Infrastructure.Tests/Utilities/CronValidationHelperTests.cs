using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Utilities;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Utilities;

public class CronValidationHelperTests
{
    [Theory]
    [InlineData("0 */5 * * * ?")]      // Every 5 minutes
    [InlineData("0 0 */2 * * ?")]      // Every 2 hours
    [InlineData("0 0 0/4 * * ?")]      // Every 4 hours
    [InlineData("*/30 * * * * ?")]     // Every 30 seconds
    public void ValidateCronExpression_ValidExpression_DoesNotThrow(string cronExpression)
    {
        // Act & Assert
        Should.NotThrow(() => CronValidationHelper.ValidateCronExpression(cronExpression));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("* * *")]
    [InlineData("0 0 0 0 0 0 0")]
    [InlineData("not a cron")]
    public void ValidateCronExpression_InvalidSyntax_ThrowsValidationException(string cronExpression)
    {
        // Act & Assert
        var exception = Should.Throw<ValidationException>(
            () => CronValidationHelper.ValidateCronExpression(cronExpression));
        exception.Message.ShouldContain("Invalid cron expression");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCronExpression_NullOrEmpty_ThrowsValidationException(string? cronExpression)
    {
        // Act & Assert
        var exception = Should.Throw<ValidationException>(
            () => CronValidationHelper.ValidateCronExpression(cronExpression!));
        exception.Message.ShouldContain("cannot be null or empty");
    }

    [Theory]
    [InlineData("*/1 * * * * ?")]   // Every 1 second
    [InlineData("*/5 * * * * ?")]   // Every 5 seconds
    [InlineData("*/10 * * * * ?")]  // Every 10 seconds
    [InlineData("*/15 * * * * ?")]  // Every 15 seconds
    public void ValidateCronExpression_TriggersTooFast_ThrowsValidationException(string cronExpression)
    {
        // Act & Assert - minimum is 30 seconds
        var exception = Should.Throw<ValidationException>(
            () => CronValidationHelper.ValidateCronExpression(cronExpression));
        exception.Message.ShouldContain("minimum");
    }

    [Theory]
    [InlineData("0 0 0 * * ?")]    // Once per day (24 hours)
    [InlineData("0 0 0 1 * ?")]    // Once per month
    public void ValidateCronExpression_TriggersTooSlow_ThrowsValidationException(string cronExpression)
    {
        // Act & Assert - maximum is 6 hours
        var exception = Should.Throw<ValidationException>(
            () => CronValidationHelper.ValidateCronExpression(cronExpression));
        exception.Message.ShouldContain("maximum");
    }

    [Theory]
    [InlineData("*/1 * * * * ?")]   // Every 1 second - too fast for other jobs
    [InlineData("*/5 * * * * ?")]   // Every 5 seconds - too fast for other jobs
    public void ValidateCronExpression_MalwareBlocker_HasDifferentLimits(string cronExpression)
    {
        // Act & Assert - MalwareBlocker allows faster triggers (no minimum)
        Should.NotThrow(() => CronValidationHelper.ValidateCronExpression(cronExpression, JobType.MalwareBlocker));
    }

    [Fact]
    public void ValidateCronExpression_AtExactMinimumInterval_DoesNotThrow()
    {
        // Arrange - exactly 30 seconds
        const string cronExpression = "*/30 * * * * ?";

        // Act & Assert
        Should.NotThrow(() => CronValidationHelper.ValidateCronExpression(cronExpression));
    }

    [Fact]
    public void ValidateCronExpression_AtExactMaximumInterval_DoesNotThrow()
    {
        // Arrange - exactly 6 hours
        const string cronExpression = "0 0 */6 * * ?";

        // Act & Assert
        Should.NotThrow(() => CronValidationHelper.ValidateCronExpression(cronExpression));
    }

    [Fact]
    public void ValidateCronExpression_NullJobType_UsesDefaultLimits()
    {
        // Arrange - 5 seconds would fail default limits but pass MalwareBlocker
        const string cronExpression = "*/5 * * * * ?";

        // Act & Assert - should fail because null uses default limits (30 second minimum)
        var exception = Should.Throw<ValidationException>(
            () => CronValidationHelper.ValidateCronExpression(cronExpression, null));
        exception.Message.ShouldContain("minimum");
    }

    [Theory]
    [InlineData(JobType.QueueCleaner)]
    [InlineData(JobType.DownloadCleaner)]
    [InlineData(JobType.BlacklistSynchronizer)]
    public void ValidateCronExpression_NonMalwareBlockerJobs_EnforceMinimumLimit(JobType jobType)
    {
        // Arrange - 5 seconds is below minimum
        const string cronExpression = "*/5 * * * * ?";

        // Act & Assert
        var exception = Should.Throw<ValidationException>(
            () => CronValidationHelper.ValidateCronExpression(cronExpression, jobType));
        exception.Message.ShouldContain("minimum");
    }

    [Theory]
    [InlineData("0 0 */1 * * ?")]  // Every 1 hour
    [InlineData("0 */30 * * * ?")] // Every 30 minutes
    [InlineData("0 */1 * * * ?")]  // Every 1 minute
    public void ValidateCronExpression_WithinValidRange_DoesNotThrow(string cronExpression)
    {
        // Act & Assert
        Should.NotThrow(() => CronValidationHelper.ValidateCronExpression(cronExpression));
    }
}
