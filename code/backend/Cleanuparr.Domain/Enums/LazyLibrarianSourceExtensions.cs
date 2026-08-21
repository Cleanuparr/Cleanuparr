namespace Cleanuparr.Domain.Enums;

public static class LazyLibrarianSourceExtensions
{
    private static readonly HashSet<LazyLibrarianSource> TorrentClients =
    [
        LazyLibrarianSource.QBittorrent,
        LazyLibrarianSource.Transmission,
        LazyLibrarianSource.DelugeWebUi,
        LazyLibrarianSource.DelugeRpc,
        LazyLibrarianSource.UTorrent,
        LazyLibrarianSource.RTorrent,
    ];

    /// <summary>
    /// A blackhole or Synology row keeps a torrent NZBmode but never reaches a client we can query.
    /// Its DownloadID is a path or a task id, so it collides across unrelated rows.
    /// </summary>
    public static bool IsTorrentClient(this LazyLibrarianSource source) => TorrentClients.Contains(source);

    public static string ToWireValue(this LazyLibrarianSource source) =>
        LazyLibrarianSourceConverter.ToWireValue(source);
}
