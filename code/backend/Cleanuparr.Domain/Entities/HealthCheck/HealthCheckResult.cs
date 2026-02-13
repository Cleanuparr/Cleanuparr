namespace Cleanuparr.Domain.Entities.HealthCheck;

public sealed record HealthCheckResult
{
    public bool IsHealthy { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public TimeSpan ResponseTime { get; set; }
}