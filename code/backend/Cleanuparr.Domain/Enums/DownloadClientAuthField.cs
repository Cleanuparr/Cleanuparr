namespace Cleanuparr.Domain.Enums;

/// <summary>
/// A credential field a download client's connection form can ask for.
/// </summary>
public enum DownloadClientAuthField
{
    Username,
    Password,
    ApiKey,

    /// <summary>
    /// Reverse proxy username for qBittorrent, Deluge and SABnzbd.
    /// </summary>
    HttpAuthUsername,

    /// <summary>
    /// Reverse proxy password for qBittorrent, Deluge and SABnzbd.
    /// </summary>
    HttpAuthPassword,
}
