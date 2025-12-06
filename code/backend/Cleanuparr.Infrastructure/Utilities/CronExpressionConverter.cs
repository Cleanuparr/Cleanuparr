using System.ComponentModel.DataAnnotations;
using Cleanuparr.Infrastructure.Models;
using Quartz;

namespace Cleanuparr.Infrastructure.Utilities;

/// <summary>
/// Utility for converting user-friendly schedule formats to Quartz cron expressions
/// </summary>
public static class CronExpressionConverter
{
    /// <summary>
    /// Converts a JobSchedule to a Quartz cron expression
    /// </summary>
    /// <param name="schedule">The job schedule to convert</param>
    /// <returns>A valid Quartz cron expression</returns>
    /// <exception cref="ArgumentException">Thrown when the schedule has invalid values</exception>
    public static string ConvertToCronExpression(JobSchedule schedule)
    {
        if (schedule == null)
            throw new ArgumentNullException(nameof(schedule));

        // Validate the schedule using predefined valid values
        if (!ScheduleOptions.IsValidValue(schedule.Type, schedule.Every))
        {
            var validValues = string.Join(", ", ScheduleOptions.GetValidValues(schedule.Type));
            throw new ValidationException($"Invalid value for {schedule.Type}: {schedule.Every}. Valid values are: {validValues}");
        }

        // Cron format: Seconds Minutes Hours Day-of-month Month Day-of-week Year
        return schedule.Type switch
        {
            ScheduleUnit.Seconds => 
                $"0/{schedule.Every} * * ? * * *", // Every n seconds
            
            ScheduleUnit.Minutes => 
                $"0 0/{schedule.Every} * ? * * *", // Every n minutes
            
            ScheduleUnit.Hours => 
                $"0 0 0/{schedule.Every} ? * * *", // Every n hours
            
            _ => throw new ArgumentException($"Invalid schedule unit: {schedule.Type}")
        };
    }
    
    /// <summary>
    /// Validates a cron expression string to ensure it's valid for Quartz.NET
    /// </summary>
    /// <param name="cronExpression">The cron expression to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidCronExpression(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return false;
        }
            
        return CronExpression.IsValidExpression(cronExpression);
    }
}
