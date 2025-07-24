using Cleanuparr.Domain.Entities.UTorrent.Request;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// Interface for raw HTTP communication with µTorrent Web UI API
/// Handles low-level HTTP requests and authentication token retrieval
/// </summary>
public interface IUTorrentHttpService
{
    /// <summary>
    /// Sends a raw HTTP request to the µTorrent API
    /// </summary>
    /// <param name="request">The request to send</param>
    /// <param name="guidCookie">The GUID cookie for authentication</param>
    /// <returns>Raw JSON response from the API</returns>
    Task<string> SendRawRequestAsync(UTorrentRequest request, string guidCookie);

    /// <summary>
    /// Retrieves authentication token and GUID cookie from µTorrent
    /// </summary>
    /// <returns>Tuple containing the authentication token and GUID cookie</returns>
    Task<(string token, string guidCookie)> GetTokenAndCookieAsync();
}
