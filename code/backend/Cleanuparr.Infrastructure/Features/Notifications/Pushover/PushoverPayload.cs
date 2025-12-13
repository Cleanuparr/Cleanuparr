namespace Cleanuparr.Infrastructure.Features.Notifications.Pushover;

public sealed record PushoverPayload
{
    /// <summary>
    /// Application API token (required)
    /// </summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// User/group key (required)
    /// </summary>
    public string User { get; init; } = string.Empty;

    /// <summary>
    /// Message body (required, max 1024 chars)
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Message title (optional, max 250 chars)
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Target devices (comma-separated)
    /// </summary>
    public string? Device { get; init; }

    /// <summary>
    /// Priority level (-2 to 2)
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Notification sound
    /// </summary>
    public string? Sound { get; init; }

    /// <summary>
    /// Retry interval for emergency priority (min 30 seconds)
    /// </summary>
    public int? Retry { get; init; }

    /// <summary>
    /// Expiration for emergency priority (max 10800 seconds)
    /// </summary>
    public int? Expire { get; init; }

    /// <summary>
    /// Tags for receipt tracking
    /// </summary>
    public string? Tags { get; init; }
}
