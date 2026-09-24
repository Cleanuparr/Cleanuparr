namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed record OidcCallbackResult
{
    public required bool Success { get; init; }
    public string? Subject { get; init; }
    public string? PreferredUsername { get; init; }
    public string? Email { get; init; }
    public string? Error { get; init; }

    /// <summary>
    /// The user ID of the authenticated user who initiated this OIDC flow.
    /// Set when the flow is started from an authenticated context (e.g., account linking).
    /// Used to verify the callback is completing the correct user's flow.
    /// </summary>
    public string? InitiatorUserId { get; init; }
}
