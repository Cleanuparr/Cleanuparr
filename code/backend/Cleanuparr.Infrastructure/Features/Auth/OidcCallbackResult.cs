namespace Cleanuparr.Infrastructure.Features.Auth;

/// <summary>
/// Result from processing an OIDC callback from the authentication provider.
/// </summary>
public sealed record OidcCallbackResult
{
    /// <summary>
    /// Whether the OIDC authentication succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Unique subject identifier from the OIDC provider.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Preferred username from the OIDC provider.
    /// </summary>
    public string? PreferredUsername { get; init; }

    /// <summary>
    /// Email address from the OIDC provider.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Error message if authentication failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// User ID of the authenticated user who initiated this OIDC flow.
    /// Set when the flow is started from an authenticated context (e.g., account linking).
    /// Used to verify the callback is completing the correct user's flow.
    /// </summary>
    public string? InitiatorUserId { get; init; }
}
