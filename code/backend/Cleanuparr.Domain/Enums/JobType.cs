namespace Cleanuparr.Domain.Enums;

public enum JobType
{
    QueueCleaner,
    MalwareBlocker,
    DownloadCleaner,
    BlacklistSynchronizer,
    Seeker,
    CustomFormatScoreSyncer,

    /// <summary>
    /// Text this build does not know.
    /// </summary>
    Unknown = EnumSentinel.UnknownValue,
}
