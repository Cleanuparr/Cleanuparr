using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Aggregated application statistics for dashboard integrations
/// </summary>
public class StatsResponse
{
    /// <summary>
    /// Event statistics within the timeframe
    /// </summary>
    public EventStats Events { get; set; } = new();

    /// <summary>
    /// Strike statistics within the timeframe
    /// </summary>
    public StrikeStats Strikes { get; set; } = new();

    /// <summary>
    /// Job run statistics within the timeframe
    /// </summary>
    public JobStats Jobs { get; set; } = new();

    /// <summary>
    /// Current health status of download clients and arr instances
    /// </summary>
    public HealthStats Health { get; set; } = new();

    /// <summary>
    /// When this response was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event statistics grouped by type and severity
/// </summary>
public class EventStats
{
    /// <summary>
    /// Total number of events in the timeframe
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Events grouped by EventType
    /// </summary>
    public Dictionary<string, int> ByType { get; set; } = new();

    /// <summary>
    /// Events grouped by severity level
    /// </summary>
    public Dictionary<string, int> BySeverity { get; set; } = new();

    /// <summary>
    /// The timeframe in hours that these stats cover
    /// </summary>
    public int TimeframeHours { get; set; }

    /// <summary>
    /// Recent event items (only included when includeEvents > 0)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RecentEventDto>? RecentItems { get; set; }
}

/// <summary>
/// Strike statistics
/// </summary>
public class StrikeStats
{
    /// <summary>
    /// Total number of strikes in the timeframe
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Strikes grouped by StrikeType
    /// </summary>
    public Dictionary<string, int> ByType { get; set; } = new();

    /// <summary>
    /// Number of download items removed in the timeframe
    /// </summary>
    public int ItemsRemoved { get; set; }

    /// <summary>
    /// The timeframe in hours that these stats cover
    /// </summary>
    public int TimeframeHours { get; set; }

    /// <summary>
    /// Recent strike items (only included when includeStrikes > 0)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RecentStrikeDto>? RecentItems { get; set; }
}

/// <summary>
/// Job run statistics
/// </summary>
public class JobStats
{
    /// <summary>
    /// Job run stats grouped by JobType
    /// </summary>
    public Dictionary<string, JobTypeStats> ByType { get; set; } = new();

    /// <summary>
    /// The timeframe in hours that these stats cover
    /// </summary>
    public int TimeframeHours { get; set; }
}

/// <summary>
/// Statistics for a specific job type
/// </summary>
public class JobTypeStats
{
    /// <summary>
    /// Total number of runs in the timeframe
    /// </summary>
    public int TotalRuns { get; set; }

    /// <summary>
    /// Number of completed runs
    /// </summary>
    public int Completed { get; set; }

    /// <summary>
    /// Number of failed runs
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// When the last job of this type ran
    /// </summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// When this job is next scheduled to run
    /// </summary>
    public DateTime? NextRunAt { get; set; }
}

/// <summary>
/// Health status summary for all clients and instances
/// </summary>
public class HealthStats
{
    /// <summary>
    /// Health status of download clients
    /// </summary>
    public List<DownloadClientHealthDto> DownloadClients { get; set; } = [];

    /// <summary>
    /// Health status of arr instances
    /// </summary>
    public List<ArrInstanceHealthDto> ArrInstances { get; set; } = [];
}

/// <summary>
/// Health status DTO for a download client
/// </summary>
public class DownloadClientHealthDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTime LastChecked { get; set; }
    public double? ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Health status DTO for an arr instance
/// </summary>
public class ArrInstanceHealthDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTime LastChecked { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Recent event DTO for stats endpoint
/// </summary>
public class RecentEventDto
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Data { get; set; }
}

/// <summary>
/// Recent strike DTO for stats endpoint
/// </summary>
public class RecentStrikeDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string DownloadId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
