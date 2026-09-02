namespace Cleanuparr.Domain.Enums;

public enum EventType
{
    FailedImportStrike,
    StalledStrike,
    DownloadingMetadataStrike,
    SlowSpeedStrike,
    SlowTimeStrike,
    DeadTorrentStrike,
    QueueItemDeleted,
    DownloadCleaned,
    CategoryChanged,
    DownloadMarkedForDeletion,
    SearchTriggered,
    StrikeReset,

    /// <summary>Text this build does not know.</summary>
    Unknown = EnumSentinel.UnknownValue,
}
