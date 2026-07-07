namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Run stats for a single job type within the window, enriched with the next scheduled run.
/// </summary>
public class JobTypeV2Stats
{
    /// <summary>
    /// Total runs of this job type in the window.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Runs of this job type that completed successfully.
    /// </summary>
    public int Completed { get; set; }

    /// <summary>
    /// Runs of this job type that failed.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// When this job type last ran, or null if it never ran in the window.
    /// </summary>
    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>
    /// When this job type is next scheduled to run, or null if it is not scheduled.
    /// </summary>
    public DateTimeOffset? NextRunAt { get; set; }
}
