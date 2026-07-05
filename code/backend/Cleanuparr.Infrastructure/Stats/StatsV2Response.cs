namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Comprehensive v2 statistics, derived from the event stream (active events + archived history).
/// </summary>
public class StatsV2Response
{
    public EventV2Stats Events { get; set; } = new();
    public StrikeV2Stats Strikes { get; set; } = new();
    public MalwareV2Stats Malware { get; set; } = new();
    public JobV2Stats Jobs { get; set; } = new();
    public HealthStats Health { get; set; } = new();

    public DateTimeOffset GeneratedAt { get; set; }
}

/// <summary>Event counts within the window, merged across active events and archived history.</summary>
public class EventV2Stats
{
    /// <summary>Total events in the window.</summary>
    public int TotalCount { get; set; }

    /// <summary>Event counts keyed by <see cref="Domain.Enums.EventType"/>.</summary>
    public Dictionary<string, int> ByType { get; set; } = new();

    /// <summary>Event counts keyed by <see cref="Domain.Enums.EventSeverity"/>.</summary>
    public Dictionary<string, int> BySeverity { get; set; } = new();
}

public class StrikeV2Stats
{
    /// <summary>Current active strike counts by type (live enforcement table, not windowed).</summary>
    public Dictionary<string, int> Active { get; set; } = new();

    /// <summary>Strikes issued within the window (from strike events).</summary>
    public int Issued { get; set; }

    /// <summary>Downloads recovered within the window (StrikeReset events).</summary>
    public int Recovered { get; set; }

    /// <summary>Downloads removed within the window (QueueItemDeleted events).</summary>
    public int Removed { get; set; }
}

public class MalwareV2Stats
{
    /// <summary>Downloads deleted for blocked files within the window.</summary>
    public int Blocked { get; set; }
}

/// <summary>Job-run stats within the window, in total and broken down by job type.</summary>
public class JobV2Stats
{
    /// <summary>Total job runs in the window.</summary>
    public int TotalRuns { get; set; }

    /// <summary>Job runs that completed successfully.</summary>
    public int Completed { get; set; }

    /// <summary>Job runs that failed.</summary>
    public int Failed { get; set; }

    /// <summary>Per-job-type stats keyed by job type name.</summary>
    public Dictionary<string, JobTypeStats> ByType { get; set; } = new();
}

/// <summary>
/// A single time bucket in a timeline series.
/// </summary>
public class TimelineBucketDto
{
    /// <summary>The day this bucket covers (UTC).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Number of matching events on that day.</summary>
    public int Count { get; set; }
}
