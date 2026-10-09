using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// The provider type LazyLibrarian recorded for a snatch, from its NZBmode column.
/// Torrent, Torznab and Magnet go to a torrent client; Nzb goes to a usenet client when the source is SABnzbd.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<LazyLibrarianDownloadMode>))]
public enum LazyLibrarianDownloadMode
{
    Unknown = 0,
    Torrent,
    Torznab,
    Magnet,
    Nzb,
    Direct,
    Irc,
}
