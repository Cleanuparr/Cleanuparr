namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Scheduled job run outcomes for the window. Job runs are recorded regardless of dry-run mode.
/// </summary>
public class JobV2Stats
{
    /// <summary>
    /// Total job runs in the window across all job types.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Job runs that completed successfully.
    /// </summary>
    public int Completed { get; set; }

    /// <summary>
    /// Job runs that failed.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Per-job-type run stats. Keys are PascalCase job-type names (QueueCleaner, MalwareBlocker, ...).
    /// </summary>
    public Dictionary<string, JobTypeV2Stats> ByType { get; set; } = new();
}
