namespace Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;

/// <summary>
/// Types of HTTP clients that can be configured
/// </summary>
public enum HttpClientType
{
    Default,
    WithRetry,
    Deluge,
    UTorrent,
}
