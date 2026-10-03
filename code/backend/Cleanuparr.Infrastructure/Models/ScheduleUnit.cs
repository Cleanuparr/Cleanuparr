namespace Cleanuparr.Infrastructure.Models;

/// <summary>
/// Unit of time for job scheduling intervals.
/// </summary>
public enum ScheduleUnit
{
    /// <summary>
    /// Schedule interval measured in seconds.
    /// </summary>
    Seconds,

    /// <summary>
    /// Schedule interval measured in minutes.
    /// </summary>
    Minutes,

    /// <summary>
    /// Schedule interval measured in hours.
    /// </summary>
    Hours
}
