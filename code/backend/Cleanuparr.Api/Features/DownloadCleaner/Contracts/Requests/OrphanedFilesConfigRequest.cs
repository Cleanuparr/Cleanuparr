using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;

public sealed record OrphanedFilesConfigRequest
{
    public bool Enabled { get; init; }

    public List<string> ScanDirectories { get; init; } = [];

    public string? OrphanedDirectory { get; init; }

    public List<string> ExcludePatterns { get; init; } = [];

    [Range(0, int.MaxValue)]
    public int MinFileAgeMinutes { get; init; }

    [Range(1, int.MaxValue)]
    public int? EmptyAfterXDays { get; init; }
}
