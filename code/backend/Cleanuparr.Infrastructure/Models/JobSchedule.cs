using System.ComponentModel.DataAnnotations;
using Cleanuparr.Infrastructure.Utilities;

namespace Cleanuparr.Infrastructure.Models;

/// <summary>
/// Represents a user-friendly job schedule format
/// </summary>
public class JobSchedule
{
    /// <summary>
    /// The numeric interval value
    /// </summary>
    public int Every { get; set; }

    /// <summary>
    /// The unit of time for the interval (seconds, minutes, or hours)
    /// </summary>
    public ScheduleUnit Type { get; set; }

    /// <summary>
    /// Converts the JobSchedule to a Quartz cron expression string
    /// </summary>
    /// <returns>A valid cron expression string</returns>
    public string ToCronExpression()
    {
        return CronExpressionConverter.ConvertToCronExpression(this);
    }

    /// <summary>
    /// Validates the job schedule against the predefined valid values
    /// </summary>
    /// <exception cref="ValidationException">Thrown when the value is not valid for the selected unit</exception>
    public void Validate()
    {
        if (!ScheduleOptions.IsValidValue(Type, Every))
        {
            var validValues = string.Join(", ", ScheduleOptions.GetValidValues(Type));
            throw new ValidationException($"Invalid value for {Type}: {Every}. Valid values are: {validValues}");
        }
    }
}
