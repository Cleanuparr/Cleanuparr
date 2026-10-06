namespace Cleanuparr.Infrastructure.Features.Auth;

/// <summary>
/// Result from initiating an OIDC authorization flow.
/// </summary>
public sealed record OidcAuthorizationResult
{
    /// <summary>
    /// URL to redirect the user to for OIDC provider authentication.
    /// </summary>
    public required string AuthorizationUrl { get; init; }

    /// <summary>
    /// Opaque state value to protect against CSRF attacks.
    /// </summary>
    public required string State { get; init; }
}
