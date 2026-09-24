namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// What <see cref="DownloadService.ScanFilesForBlocking"/> does with one file.
/// </summary>
public enum FileBlockAction
{
    /// <summary>
    /// Already unwanted in the client; count it and skip the blocklist.
    /// </summary>
    AlreadySkipped,

    /// <summary>
    /// Check the file name against the blocklist.
    /// </summary>
    CheckBlocklist
}
