namespace Cleanuparr.Domain.Enums;

public enum InstanceType
{
    Sonarr,
    Radarr,
    Lidarr,
    Readarr,
    Whisparr,
    Sportarr,
    LazyLibrarian,
    Chaptarr,

    /// <summary>
    /// Text this build does not know.
    /// </summary>
    Unknown = EnumSentinel.UnknownValue,
}
