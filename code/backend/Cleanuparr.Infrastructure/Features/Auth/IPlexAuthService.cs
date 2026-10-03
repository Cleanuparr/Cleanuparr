namespace Cleanuparr.Infrastructure.Features.Auth;

public interface IPlexAuthService
{
    /// <summary>
    /// Creates a Plex authentication PIN and builds the URL the user is sent to in order to authorize.
    /// </summary>
    /// <param name="forwardUrl">
    /// Optional URL Plex redirects the browser back to after authorization. When omitted, no redirect
    /// is added and the caller is expected to poll <see cref="CheckPin"/> instead.
    /// </param>
    Task<PlexPinResult> RequestPin(string? forwardUrl = null);

    /// <summary>
    /// Checks whether a PIN has been authorized, returning the Plex auth token once it has.
    /// </summary>
    Task<PlexPinCheckResult> CheckPin(int pinId);

    /// <summary>
    /// Retrieves the Plex account associated with the given auth token.
    /// </summary>
    Task<PlexAccountInfo> GetAccount(string authToken);
}
