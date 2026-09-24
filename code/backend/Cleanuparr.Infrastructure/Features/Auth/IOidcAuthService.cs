namespace Cleanuparr.Infrastructure.Features.Auth;

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
