namespace Cleanuparr.Api.Features.Auth.Contracts.Responses;

/// <summary>
/// Result of polling a Plex PIN for authorization.
/// </summary>
public sealed record PlexVerifyResponse
{
    /// <summary>
    /// Whether the user has authorized the PIN on Plex.
    /// </summary>
    public required bool Completed { get; init; }

    /// <summary>
    /// Session tokens, issued only when a Plex login completes.
    /// </summary>
    public TokenResponse? Tokens { get; init; }
}
