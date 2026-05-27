using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;

public sealed record OrphanedFilesConfigRequest
{
    public bool Enabled { get; init; }

    public List<string> ScanDirectories { get; init; } = [];

    [Required]
    public string OrphanedDirectory { get; init; } = string.Empty;

    public List<string> ExcludePatterns { get; init; } = [];

    [Range(1, int.MaxValue)]
    public int MinFileAgeHours { get; init; } = 24;

    [Range(1, int.MaxValue)]
    public int? PurgeAfterHours { get; init; }
}
