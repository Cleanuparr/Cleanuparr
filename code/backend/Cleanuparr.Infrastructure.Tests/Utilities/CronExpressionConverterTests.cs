using System.ComponentModel.DataAnnotations;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Utilities;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Utilities;

public class CronExpressionConverterTests
{
    [Fact]
    public void ConvertToCronExpression_Seconds_ReturnsCorrectFormat()
    {
        // Arrange
        var schedule = new JobSchedule { Every = 30, Type = ScheduleUnit.Seconds };

        // Act
        var result = CronExpressionConverter.ConvertToCronExpression(schedule);

        // Assert
        result.ShouldBe("0/30 * * ? * * *");
    }

    [Theory]
    [InlineData(1, "0 0/1 * ? * * *")]
    [InlineData(5, "0 0/5 * ? * * *")]
    [InlineData(10, "0 0/10 * ? * * *")]
    [InlineData(15, "0 0/15 * ? * * *")]
    [InlineData(30, "0 0/30 * ? * * *")]
    public void ConvertToCronExpression_Minutes_ReturnsCorrectFormat(int minutes, string expected)
    {
        // Arrange
        var schedule = new JobSchedule { Every = minutes, Type = ScheduleUnit.Minutes };

        // Act
        var result = CronExpressionConverter.ConvertToCronExpression(schedule);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(1, "0 0 0/1 ? * * *")]
    [InlineData(2, "0 0 0/2 ? * * *")]
    [InlineData(4, "0 0 0/4 ? * * *")]
    [InlineData(6, "0 0 0/6 ? * * *")]
    [InlineData(12, "0 0 0/12 ? * * *")]
    public void ConvertToCronExpression_Hours_ReturnsCorrectFormat(int hours, string expected)
    {
        // Arrange
        var schedule = new JobSchedule { Every = hours, Type = ScheduleUnit.Hours };

        // Act
        var result = CronExpressionConverter.ConvertToCronExpression(schedule);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("0 */5 * * * ?")]
    [InlineData("0 0 */2 * * ?")]
    [InlineData("0/30 * * ? * * *")]
    public void IsValidCronExpression_ValidQuartzCron_ReturnsTrue(string cronExpression)
    {
        // Act
        var result = CronExpressionConverter.IsValidCronExpression(cronExpression);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("* * *")]
    [InlineData("not a cron")]
    [InlineData("0 0 0 0 0 0 0")]
    public void IsValidCronExpression_InvalidCron_ReturnsFalse(string cronExpression)
    {
        // Act
        var result = CronExpressionConverter.IsValidCronExpression(cronExpression);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void GetCronDescription_ValidExpression_ReturnsDescription()
    {
        // Arrange
        const string cronExpression = "0 */5 * * * ?";

        // Act
        var result = CronExpressionConverter.GetCronDescription(cronExpression);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain(cronExpression);
    }

    [Fact]
    public void GetCronDescription_InvalidExpression_ReturnsNull()
    {
        // Arrange
        const string cronExpression = "invalid";

        // Act
        var result = CronExpressionConverter.GetCronDescription(cronExpression);

        // Assert
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void ConvertToCronExpression_AllValidMinuteValues_Succeeds(int minutes)
    {
        // Arrange
        var schedule = new JobSchedule { Every = minutes, Type = ScheduleUnit.Minutes };

        // Act & Assert
        Should.NotThrow(() => CronExpressionConverter.ConvertToCronExpression(schedule));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(12)]
    public void ConvertToCronExpression_AllValidHourValues_Succeeds(int hours)
    {
        // Arrange
        var schedule = new JobSchedule { Every = hours, Type = ScheduleUnit.Hours };

        // Act & Assert
        Should.NotThrow(() => CronExpressionConverter.ConvertToCronExpression(schedule));
    }

    [Theory]
    [InlineData(7, ScheduleUnit.Minutes)]   // 7 doesn't divide 60 evenly
    [InlineData(45, ScheduleUnit.Minutes)]  // 45 is not in the valid list
    [InlineData(5, ScheduleUnit.Hours)]     // 5 doesn't divide 24 evenly
    [InlineData(7, ScheduleUnit.Hours)]     // 7 doesn't divide 24 evenly
    [InlineData(15, ScheduleUnit.Seconds)]  // Only 30 seconds is valid
    public void ConvertToCronExpression_InvalidValue_ThrowsValidationException(int value, ScheduleUnit unit)
    {
        // Arrange
        var schedule = new JobSchedule { Every = value, Type = unit };

        // Act & Assert
        Should.Throw<ValidationException>(() => CronExpressionConverter.ConvertToCronExpression(schedule));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsValidCronExpression_EmptyOrNull_ReturnsFalse(string? cronExpression)
    {
        // Act
        var result = CronExpressionConverter.IsValidCronExpression(cronExpression!);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ConvertToCronExpression_NullSchedule_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => CronExpressionConverter.ConvertToCronExpression(null!));
    }
}
