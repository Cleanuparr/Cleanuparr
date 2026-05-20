using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cleanuparr.Persistence.Models.Configuration.OrphanedFilesCleaner;

public sealed record OrphanedFilesCleanerConfig : IJobConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool Enabled { get; set; }

    public string CronExpression { get; set; } = "0 0 * * * ?";

    public bool UseAdvancedScheduling { get; set; }

    /// <summary>
    /// Directories to scan for orphaned files.
    /// </summary>
    public List<string> ScanDirectories { get; set; } = [];

    /// <summary>
    /// Directory where orphaned files are moved.
    /// If null or empty, orphaned files are logged but not moved.
    /// </summary>
    public string? OrphanedDirectory { get; set; }

    /// <summary>
    /// Source path prefix reported by the download client (e.g. /downloads).
    /// Used for path remapping when paths differ between the container and the host.
    /// </summary>
    public string? DownloadDirectorySource { get; set; }

    /// <summary>
    /// Target path prefix on the local filesystem (e.g. /mnt/data).
    /// Used for path remapping when paths differ between the container and the host.
    /// </summary>
    public string? DownloadDirectoryTarget { get; set; }

    /// <summary>
    /// Glob patterns for file/folder names to skip (e.g. "*.nfo", ".DS_Store").
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = [];

    /// <summary>
    /// Minimum age in minutes a file or folder must have before it is considered orphaned.
    /// Protects files that are still being actively downloaded.
    /// </summary>
    public int MinFileAgeMinutes { get; set; } = 0;

    /// <summary>
    /// Maximum number of orphaned entries to move per run.
    /// Acts as a safety cap to prevent accidental mass moves.
    /// </summary>
    public int MaxOrphanedFilesToProcess { get; set; } = 50;

    /// <summary>
    /// If set, entries in OrphanedDirectory older than this many days are permanently deleted.
    /// When null, orphaned entries are kept indefinitely.
    /// </summary>
    public int? EmptyAfterXDays { get; set; }

    public void Validate()
    {
    }
}
