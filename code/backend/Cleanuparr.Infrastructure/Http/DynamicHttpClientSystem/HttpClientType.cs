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

    /// <summary>
    /// Carries the User-Agent and nothing else.
    /// No primary handler, no timeout override, no retry policy.
    /// </summary>
    Plain,
}
