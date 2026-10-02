namespace Cleanuparr.Infrastructure.Features.Auth;

/// <summary>
/// User account information obtained from Plex authentication.
/// </summary>
public sealed record PlexAccountInfo
{
    /// <summary>
    /// Plex account ID.
    /// </summary>
    public required string AccountId { get; init; }

    /// <summary>
    /// Plex username.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Email address associated with the Plex account.
    /// </summary>
    public string? Email { get; init; }
}
