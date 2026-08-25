namespace Cleanuparr.Domain.Enums;

public enum DownloadClientTypeName
{
    qBittorrent,
    Deluge,
    Transmission,
    uTorrent,
    rTorrent,

    /// <summary>Text this build does not know.</summary>
    Unknown = EnumSentinel.UnknownValue,
}
