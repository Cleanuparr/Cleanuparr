using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;

public record UpdateDownloadCleanerConfigRequest
{
    public bool Enabled { get; init; }

    public string CronExpression { get; init; } = "0 0 * * * ?";

    /// <summary>
    /// Indicates whether to use the CronExpression directly or convert from a user-friendly schedule.
    /// </summary>
    public bool UseAdvancedScheduling { get; init; }

    public List<CleanCategoryRequest> Categories { get; init; } = [];

    public bool DeletePrivate { get; init; }
    
    /// <summary>
    /// Indicates whether unlinked download handling is enabled.
    /// </summary>
    public bool UnlinkedEnabled { get; init; }
    
    public string UnlinkedTargetCategory { get; init; } = "cleanuparr-unlinked";

    public bool UnlinkedUseTag { get; init; }

    public string UnlinkedIgnoredRootDir { get; init; } = string.Empty;
    
    public List<string> UnlinkedCategories { get; init; } = [];

    public List<string> IgnoredDownloads { get; init; } = [];
}

public record CleanCategoryRequest
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
}
