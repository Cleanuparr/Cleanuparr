namespace Cleanuparr.Api.Features.OrphanedFilesCleanup.Contracts.Requests;

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
}
