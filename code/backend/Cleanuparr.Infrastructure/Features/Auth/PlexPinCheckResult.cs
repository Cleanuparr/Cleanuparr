namespace Cleanuparr.Infrastructure.Features.Auth;

/// <summary>
/// Result from checking the status of a Plex PIN authorization request.
/// </summary>
public sealed record PlexPinCheckResult
{
    /// <summary>
    /// Whether the user has completed the PIN authentication flow.
    /// </summary>
    public required bool Completed { get; init; }

    /// <summary>
    /// Authentication token if the PIN flow is completed.
    /// </summary>
    public string? AuthToken { get; init; }
}
