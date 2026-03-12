namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed record OidcAuthorizationResult
{
    public required string AuthorizationUrl { get; init; }
    public required string State { get; init; }
}

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

public interface IOidcAuthService
{
    /// <summary>
    /// Generates the OIDC authorization URL and stores state/verifier for the callback.
    /// </summary>
    /// <param name="redirectUri">The callback URI for the OIDC provider.</param>
    /// <param name="initiatorUserId">Optional user ID of the authenticated user initiating the flow (for account linking).</param>
    Task<OidcAuthorizationResult> StartAuthorization(string redirectUri, string? initiatorUserId = null);

    /// <summary>
    /// Handles the OIDC callback: validates state, exchanges code for tokens, validates the ID token.
    /// </summary>
    Task<OidcCallbackResult> HandleCallback(string code, string state, string redirectUri);

    /// <summary>
    /// Stores tokens associated with a one-time exchange code.
    /// Returns the one-time code.
    /// </summary>
    string StoreOneTimeCode(string accessToken, string refreshToken, int expiresIn);

    /// <summary>
    /// Exchanges a one-time code for the stored tokens.
    /// The code is consumed (can only be used once).
    /// </summary>
    OidcTokenExchangeResult? ExchangeOneTimeCode(string code);
}

public sealed record OidcTokenExchangeResult
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required int ExpiresIn { get; init; }
}
