namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Aggregated application statistics for a time window, designed for dashboard integrations.
/// Every section except <see cref="Health"/> is scoped to the window and reflects only live activity
/// unless dry-run is explicitly included. <see cref="Health"/> is a point-in-time gauge.
/// </summary>
public class StatsV2Response
{
    /// <summary>
    /// The raw event audit for the window: total plus breakdowns by event type and severity.
    /// Higher-level sections (strikes, removals, cleaned, searches) are ergonomic roll-ups derived
    /// from the same events.
    /// </summary>
    public EventV2Stats Events { get; set; } = new();

    /// <summary>
    /// Strike activity in the window (issued, by type, recovered).
    /// </summary>
    public StrikeV2Stats Strikes { get; set; } = new();

    /// <summary>
    /// Downloads removed in the window, broken down by reason. Malware removals are the
    /// AllFilesBlocked + AtLeastOneFileBlocked reasons within this breakdown.
    /// </summary>
    public RemovalsV2Stats Removals { get; set; } = new();

    /// <summary>
    /// Downloads cleaned by the download cleaner in the window, broken down by reason.
    /// </summary>
    public CleanedV2Stats Cleaned { get; set; } = new();

    /// <summary>
    /// Seeker search activity in the window.
    /// </summary>
    public SearchesV2Stats Searches { get; set; } = new();

    /// <summary>
    /// Scheduled job run outcomes in the window, overall and per job type.
    /// </summary>
    public JobV2Stats Jobs { get; set; } = new();

    /// <summary>
    /// Current health of configured download clients and arr instances. This is a cached gauge
    /// (updated by a background service roughly every 5 minutes), not a windowed metric.
    /// </summary>
    public HealthStats Health { get; set; } = new();

    /// <summary>
    /// The window the response covers, in hours, echoing the requested value after clamping.
    /// </summary>
    public int WindowHours { get; set; }

    /// <summary>
    /// When this response was generated (UTC).
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; }
}
