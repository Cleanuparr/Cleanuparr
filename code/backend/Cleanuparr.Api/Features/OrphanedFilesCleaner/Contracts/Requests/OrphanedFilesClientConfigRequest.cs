namespace Cleanuparr.Api.Features.OrphanedFilesCleaner.Contracts.Requests;

public sealed record OrphanedFilesClientConfigRequest
{
    public bool Enabled { get; init; }

    /// <summary>
    /// Directories to scan for orphaned files on this download client.
    /// </summary>
    public List<string> ScanDirectories { get; init; } = [];

    /// <summary>
    /// Directory where orphaned files are moved.
    /// If null or empty, orphaned files are logged but not moved.
    /// </summary>
    public string? OrphanedDirectory { get; init; }

    /// <summary>
    /// Source path prefix reported by this download client.
    /// </summary>
    public string? DownloadDirectorySource { get; init; }

    /// <summary>
    /// Target path prefix on the local filesystem.
    /// </summary>
    public string? DownloadDirectoryTarget { get; init; }
}
