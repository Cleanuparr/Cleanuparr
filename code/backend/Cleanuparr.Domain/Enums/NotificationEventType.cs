namespace Cleanuparr.Domain.Enums;

public enum NotificationEventType
{
    Test,
    FailedImportStrike,
    StalledStrike,
    SlowSpeedStrike,
    SlowTimeStrike,
    QueueItemDeleted,
    DownloadCleaned,
    CategoryChanged
}
