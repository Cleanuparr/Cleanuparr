namespace Cleanuparr.Domain.Enums;

public static class LazyLibrarianSourceExtensions
{
    private static readonly Dictionary<LazyLibrarianSource, DownloadClientType> QueryableClients = new()
    {
        [LazyLibrarianSource.QBittorrent] = DownloadClientType.Torrent,
        [LazyLibrarianSource.Transmission] = DownloadClientType.Torrent,
        [LazyLibrarianSource.DelugeWebUi] = DownloadClientType.Torrent,
        [LazyLibrarianSource.DelugeRpc] = DownloadClientType.Torrent,
        [LazyLibrarianSource.UTorrent] = DownloadClientType.Torrent,
        [LazyLibrarianSource.RTorrent] = DownloadClientType.Torrent,
        [LazyLibrarianSource.Sabnzbd] = DownloadClientType.Usenet,
    };

    /// <summary>
    /// The Cleanuparr client type that can query this source, or null when it never reaches a client we can query
    /// (blackhole, Synology, direct, irc, or an unsupported usenet downloader like NZBGet).
    /// Its DownloadID is then a path or a task id, so it collides across unrelated rows.
    /// </summary>
    public static DownloadClientType? ClientType(this LazyLibrarianSource source) =>
        QueryableClients.TryGetValue(source, out DownloadClientType type) ? type : null;

    public static string ToWireValue(this LazyLibrarianSource source) =>
        LazyLibrarianSourceConverter.ToWireValue(source);
}
