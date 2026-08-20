using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// The downloader LazyLibrarian sent a snatch to, from the Source column.
/// The value round-trips: getDownloadProgress takes it back verbatim.
/// </summary>
[JsonConverter(typeof(LazyLibrarianSourceConverter))]
public enum LazyLibrarianSource
{
    Unknown = 0,
    QBittorrent,
    Transmission,
    DelugeWebUi,
    DelugeRpc,
    UTorrent,
    RTorrent,
    Blackhole,
    Direct,
    Irc,
    Synology,
    SynologyTorrent,
    SynologyNzb,
    Sabnzbd,
    NzbGet,
}
