namespace Cleanuparr.Api.Features.OrphanedFilesCleanup.Contracts.Requests;

public sealed record UpdateOrphanedFilesCleanupConfigRequest
{
    /// <summary>
    /// Glob patterns for file/folder names to skip (e.g. "*.nfo", ".DS_Store").
    /// </summary>
    public List<string> ExcludePatterns { get; init; } = [];

    /// <summary>
    /// Minimum age in minutes a file or folder must have before it is considered orphaned.
    /// </summary>
    public int MinFileAgeMinutes { get; init; } = 0;

    /// <summary>
    /// If set, entries in OrphanedDirectory older than this many days are permanently deleted.
    /// </summary>
    public int? EmptyAfterXDays { get; init; }
}
