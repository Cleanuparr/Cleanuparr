namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Cached health snapshot for a single arr instance.
/// </summary>
public class ArrInstanceHealthDto
{
    /// <summary>
    /// Unique identifier of the arr instance.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name of the arr instance.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Instance type (Sonarr, Radarr, Lidarr, Readarr, Whisparr).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether the last health check succeeded.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// When the last health check ran (UTC).
    /// </summary>
    public DateTimeOffset LastChecked { get; set; }

    /// <summary>
    /// Error message from the last health check, or null when healthy.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
