namespace Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;

/// <summary>
/// Retry configuration for HTTP clients
/// </summary>
public class RetryConfig
{
    public int MaxRetries { get; set; }
    public bool ExcludeUnauthorized { get; set; } = true;
}
