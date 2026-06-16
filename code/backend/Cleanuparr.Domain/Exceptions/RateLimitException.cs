namespace Cleanuparr.Domain.Exceptions;

/// <summary>
/// Thrown when a request is rejected due to rate limiting. Maps to HTTP 429 with a
/// <c>retryAfterSeconds</c> problem-details extension and a <c>Retry-After</c> header.
/// </summary>
public sealed class RateLimitException : Exception
{
    public int RetryAfterSeconds { get; }

    public RateLimitException(string message, int retryAfterSeconds = 0) : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public RateLimitException(string message, Exception inner, int retryAfterSeconds = 0) : base(message, inner)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
