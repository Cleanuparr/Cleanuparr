using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;

public sealed record OrphanedFilesClientConfigRequest
{
    public bool Enabled { get; init; }

    public List<string> ScanDirectories { get; init; } = [];

    /// <summary>
    /// If null or empty, orphaned files are logged but not moved.
    /// </summary>
    public string? OrphanedDirectory { get; init; }

    public List<string> ExcludePatterns { get; init; } = [];

    [Range(0, int.MaxValue)]
    public int MinFileAgeMinutes { get; init; }

    [Range(1, int.MaxValue)]
    public int? EmptyAfterXDays { get; init; }
}
