using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Shared.Helpers;
using Quartz;
using Quartz.Spi;

namespace Cleanuparr.Infrastructure.Utilities;

/// <summary>
/// Helper class for validating cron expressions including trigger interval limits
/// </summary>
public static class CronValidationHelper
{
    /// <summary>
    /// Validates a cron expression against the application's trigger limits
    /// </summary>
    /// <param name="cronExpression">The cron expression to validate</param>
    /// <exception cref="ValidationException">Thrown when the cron expression is invalid or violates trigger limits</exception>
    public static void ValidateCronExpression(string cronExpression, JobType? jobType = null)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new ValidationException("Cron expression cannot be null or empty");
        }

        // First check if it's a valid cron expression
        if (!CronExpression.IsValidExpression(cronExpression))
        {
            throw new ValidationException($"Invalid cron expression: {cronExpression}");
        }

        // Validate the trigger timing limits
        try
        {
            IOperableTrigger triggerObj = (IOperableTrigger)TriggerBuilder.Create()
                .WithIdentity("ValidationTrigger")
                .StartNow()
                .WithCronSchedule(cronExpression, x => x.WithMisfireHandlingInstructionDoNothing())
                .Build();

            IReadOnlyList<DateTimeOffset> nextFireTimes = TriggerUtils.ComputeFireTimes(triggerObj, null, 2);
            
            if (nextFireTimes.Count < 2)
            {
                throw new ValidationException($"Could not compute fire times for cron expression: {cronExpression}");
            }
            
            TimeSpan triggerValue = nextFireTimes[1] - nextFireTimes[0];
            
            if (triggerValue > Constants.TriggerMaxLimit)
            {
                throw new ValidationException($"{cronExpression} should have a fire time of maximum {Constants.TriggerMaxLimit.TotalHours} hours");
            }
            
            if (jobType is not JobType.MalwareBlocker && triggerValue < Constants.TriggerMinLimit)
            {
                throw new ValidationException($"{cronExpression} should have a fire time of minimum {Constants.TriggerMinLimit.TotalSeconds} seconds");
            }
        }
        catch (ValidationException)
        {
            // Re-throw validation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            throw new ValidationException($"Error validating cron expression '{cronExpression}': {ex.Message}");
        }
    }
} 