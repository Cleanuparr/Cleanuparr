namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// What <see cref="DownloadService.ScanForHardLinks"/> does with one file.
/// </summary>
public enum HardLinkScanAction
{
    /// <summary>
    /// File not downloaded; log and skip it.
    /// </summary>
    SkipUnwanted,

    /// <summary>
    /// Read the file's hardlink count from disk.
    /// </summary>
    CheckHardLinks,

    /// <summary>
    /// Count the torrent as hardlinked without touching disk.
    /// </summary>
    TreatAsLinked
}
