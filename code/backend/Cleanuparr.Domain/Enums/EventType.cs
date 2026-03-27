namespace Cleanuparr.Domain.Enums;

public enum EventType
{
    FailedImportStrike,
    StalledStrike,
    DownloadingMetadataStrike,
    SlowSpeedStrike,
    SlowTimeStrike,
    QueueItemDeleted,
    DownloadCleaned,
    CategoryChanged,
    DownloadMarkedForDeletion,
    SearchTriggered,
}