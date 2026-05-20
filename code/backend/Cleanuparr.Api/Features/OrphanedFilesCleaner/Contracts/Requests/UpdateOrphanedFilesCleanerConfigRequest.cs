namespace Cleanuparr.Api.Features.OrphanedFilesCleaner.Contracts.Requests;

public sealed record UpdateOrphanedFilesCleanerConfigRequest
{
    public bool Enabled { get; init; }

    public string CronExpression { get; init; } = "0 0 * * * ?";

    /// <summary>
    /// Indicates whether to use the CronExpression directly or convert from a user-friendly schedule.
    /// </summary>
    public bool UseAdvancedScheduling { get; init; }

    /// <summary>
    /// Directories to scan for orphaned files.
    /// </summary>
    public List<string> ScanDirectories { get; init; } = [];

    /// <summary>
    /// Directory where orphaned files are moved.
    /// If null or empty, orphaned files are logged but not moved.
    /// </summary>
    public string? OrphanedDirectory { get; init; }

    /// <summary>
    /// Source path prefix reported by the download client.
    /// </summary>
    public string? DownloadDirectorySource { get; init; }

    /// <summary>
    /// Target path prefix on the local filesystem.
    /// </summary>
    public string? DownloadDirectoryTarget { get; init; }

    /// <summary>
    /// Glob patterns for file/folder names to skip (e.g. "*.nfo", ".DS_Store").
    /// </summary>
    public List<string> ExcludePatterns { get; init; } = [];

    /// <summary>
    /// Minimum age in minutes a file or folder must have before it is considered orphaned.
    /// </summary>
    public int MinFileAgeMinutes { get; init; } = 0;

    /// <summary>
    /// Maximum number of orphaned entries to move per run.
    /// </summary>
    public int MaxOrphanedFilesToProcess { get; init; } = 50;

    /// <summary>
    /// If set, entries in OrphanedDirectory older than this many days are permanently deleted.
    /// </summary>
    public int? EmptyAfterXDays { get; init; }
}
