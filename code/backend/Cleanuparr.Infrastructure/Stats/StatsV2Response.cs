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

    /// <summary>The requested window (e.g. "24h", "7d", "30d", "1y").</summary>
    public string Window { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; set; }
}

public class EventV2Stats
{
    public int TotalCount { get; set; }
    public Dictionary<string, int> ByType { get; set; } = new();
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

public class JobV2Stats
{
    public int TotalRuns { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public Dictionary<string, JobTypeStats> ByType { get; set; } = new();
}

/// <summary>
/// A single time bucket in a timeline series.
/// </summary>
public class TimelineBucketDto
{
    public DateOnly Date { get; set; }
    public int Count { get; set; }
}
