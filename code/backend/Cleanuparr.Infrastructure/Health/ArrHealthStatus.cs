using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Health;

/// <summary>
/// Represents the health status of an arr instance
/// </summary>
public class ArrHealthStatus
{
    /// <summary>
    /// Gets or sets the instance ID
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the instance name
    /// </summary>
    public string InstanceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the instance type (Sonarr, Radarr, etc.)
    /// </summary>
    public InstanceType InstanceType { get; set; }

    /// <summary>
    /// Gets or sets whether the instance is healthy
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the time when the instance was last checked
    /// </summary>
    public DateTime LastChecked { get; set; }

    /// <summary>
    /// Gets or sets the error message if the instance is not healthy
    /// </summary>
    public string? ErrorMessage { get; set; }
}
