namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

/// <summary>
/// Custom format score statistics for one arr instance.
/// </summary>
public sealed record InstanceCfScoreStat
{
    /// <summary>
    /// Unique identifier of the *arr instance.
    /// </summary>
    public Guid InstanceId { get; init; }

    /// <summary>
    /// Display name of the *arr instance.
    /// </summary>
    public string InstanceName { get; init; } = string.Empty;

    /// <summary>
    /// Arr type of the instance, such as Sonarr or Radarr.
    /// </summary>
    public string InstanceType { get; init; } = string.Empty;

    /// <summary>
    /// Total number of items tracked by this instance.
    /// </summary>
    public int TotalTracked { get; init; }

    /// <summary>
    /// Number of items with custom format score below cutoff.
    /// </summary>
    public int BelowCutoff { get; init; }

    /// <summary>
    /// Number of items with custom format score at or above cutoff.
    /// </summary>
    public int AtOrAboveCutoff { get; init; }

    /// <summary>
    /// Number of monitored items.
    /// </summary>
    public int Monitored { get; init; }

    /// <summary>
    /// Number of unmonitored items.
    /// </summary>
    public int Unmonitored { get; init; }

    /// <summary>
    /// Score increases found in the recorded score history.
    /// </summary>
    public int RecentUpgrades { get; init; }
}
