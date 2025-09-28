namespace Cleanuparr.Infrastructure.Models;

/// <summary>
/// Represents the supported job types in the application
/// </summary>
public enum JobType
{
    QueueCleaner,
    MalwareBlocker,
    DownloadCleaner,
    BlacklistSynchronizer,
}
