using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Health;

/// <summary>
/// Represents the health status of a client
/// </summary>
public class HealthStatus
{
    /// <summary>
    /// Gets or sets whether the client is healthy
    /// </summary>
    public bool IsHealthy { get; set; }
    
    /// <summary>
    /// Gets or sets the time when the client was last checked
    /// </summary>
    public DateTimeOffset LastChecked { get; set; }
    
    /// <summary>
    /// Gets or sets the error message if the client is not healthy
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Gets or sets the response time of the last health check
    /// </summary>
    public TimeSpan ResponseTime { get; set; }
    
    /// <summary>
    /// Gets or sets the client ID
    /// </summary>
    public Guid ClientId { get; set; } = Guid.Empty;
    
    /// <summary>
    /// Gets or sets the client name
    /// </summary>
    public string ClientName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the client type
    /// </summary>
    public DownloadClientTypeName ClientTypeName { get; set; }
}
