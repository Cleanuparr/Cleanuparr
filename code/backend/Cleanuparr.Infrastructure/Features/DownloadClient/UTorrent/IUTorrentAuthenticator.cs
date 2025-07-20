namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// Interface for µTorrent authentication management with caching support
/// Handles token management and session state with multi-client support
/// </summary>
public interface IUTorrentAuthenticator
{
    /// <summary>
    /// Ensures that the client is authenticated and the token is valid
    /// </summary>
    /// <returns>True if authentication is successful</returns>
    Task<bool> EnsureAuthenticatedAsync();

    /// <summary>
    /// Gets a valid authentication token, refreshing if necessary
    /// </summary>
    /// <returns>Valid authentication token</returns>
    Task<string> GetValidTokenAsync();

    /// <summary>
    /// Gets a valid GUID cookie, refreshing if necessary
    /// </summary>
    /// <returns>Valid GUID cookie</returns>
    Task<string> GetValidGuidCookieAsync();

    /// <summary>
    /// Forces a refresh of the authentication session
    /// </summary>
    Task RefreshSessionAsync();

    /// <summary>
    /// Invalidates the cached authentication session
    /// </summary>
    Task InvalidateSessionAsync();

    /// <summary>
    /// Gets whether the client is currently authenticated
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the GUID cookie for the current session
    /// </summary>
    string GuidCookie { get; }
}
