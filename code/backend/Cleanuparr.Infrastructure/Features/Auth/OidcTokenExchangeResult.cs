namespace Cleanuparr.Infrastructure.Features.Auth;

/// <summary>
/// Tokens obtained from exchanging an OIDC authorization code.
/// </summary>
public sealed record OidcTokenExchangeResult
{
    /// <summary>
    /// JWT access token for API requests.
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// Refresh token used to obtain new access tokens.
    /// </summary>
    public required string RefreshToken { get; init; }

    /// <summary>
    /// Seconds until the access token expires.
    /// </summary>
    public required int ExpiresIn { get; init; }
}
