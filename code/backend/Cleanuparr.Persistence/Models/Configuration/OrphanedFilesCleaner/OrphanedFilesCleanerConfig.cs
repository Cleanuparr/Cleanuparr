using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cleanuparr.Persistence.Models.Configuration.OrphanedFilesCleaner;

public sealed record OrphanedFilesCleanerConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

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
    /// If set, entries in OrphanedDirectory older than this many days are permanently deleted.
    /// When null, orphaned entries are kept indefinitely.
    /// </summary>
    public int? EmptyAfterXDays { get; set; }
}
