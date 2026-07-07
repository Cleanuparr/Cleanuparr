namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Cached health snapshot for a single download client.
/// </summary>
public class DownloadClientHealthDto
{
    /// <summary>
    /// Unique identifier of the download client.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name of the download client.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Client type (qBittorrent, Transmission, Deluge, ...).
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
    /// Response time of the last health check in milliseconds, or null if unavailable.
    /// </summary>
    public double? ResponseTimeMs { get; set; }

    /// <summary>
    /// Error message from the last health check, or null when healthy.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
