using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Responses;

public sealed record OrphanedFilesConfigResponse
{
    public bool Enabled { get; init; }

    public required List<string> ScanDirectories { get; init; }

    public required string OrphanedDirectory { get; init; }

    public required List<string> ExcludePatterns { get; init; }

    public int MinFileAgeHours { get; init; }

    public int? PurgeAfterHours { get; init; }

    public static OrphanedFilesConfigResponse From(OrphanedFilesConfig config) => new()
    {
        Enabled = config.Enabled,
        ScanDirectories = config.ScanDirectories,
        OrphanedDirectory = config.OrphanedDirectory,
        ExcludePatterns = config.ExcludePatterns,
        MinFileAgeHours = config.MinFileAgeHours,
        PurgeAfterHours = config.PurgeAfterHours,
    };
}
