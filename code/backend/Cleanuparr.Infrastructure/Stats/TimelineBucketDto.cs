namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// A single point in a timeline series: the count of a metric within one bucket.
/// </summary>
public class TimelineBucketDto
{
    /// <summary>
    /// Start of the bucket (UTC). Granularity depends on the requested bucket size (hour, day, week, month).
    /// </summary>
    public DateTimeOffset Date { get; set; }

    /// <summary>
    /// Count of the metric within this bucket.
    /// </summary>
    public int Count { get; set; }
}
