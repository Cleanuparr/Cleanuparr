using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;

public record SeedingRuleRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// Max ratio before removing a download.
    /// </summary>
    public double MaxRatio { get; init; } = -1;

    /// <summary>
    /// Min number of hours to seed before removing a download, if the ratio has been met.
    /// </summary>
    public double MinSeedTime { get; init; }

    /// <summary>
    /// Number of hours to seed before removing a download.
    /// </summary>
    public double MaxSeedTime { get; init; } = -1;

    /// <summary>
    /// Whether to delete the source files when cleaning the download.
    /// </summary>
    public bool DeleteSourceFiles { get; init; } = true;
}