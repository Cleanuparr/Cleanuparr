namespace Cleanuparr.Domain.Enums;

public enum DownloadClientType
{
    Torrent,
    Usenet,

    /// <summary>
    /// Text this build does not know.
    /// </summary>
    Unknown = EnumSentinel.UnknownValue,
}
