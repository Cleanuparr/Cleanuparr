using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Models;

public class UpdateDownloadCleanerConfigDto
{
    public bool Enabled { get; set; }

    public string CronExpression { get; set; } = "0 0 * * * ?";

    /// <summary>
    /// Indicates whether to use the CronExpression directly or convert from a user-friendly schedule
    /// </summary>
    public bool UseAdvancedScheduling { get; set; }

    public List<CleanCategoryDto> Categories { get; set; } = [];

    public bool DeletePrivate { get; set; }
    
    /// <summary>
    /// Indicates whether unlinked download handling is enabled
    /// </summary>
    public bool UnlinkedEnabled { get; set; } = false;
    
    public string UnlinkedTargetCategory { get; set; } = "cleanuparr-unlinked";

    public bool UnlinkedUseTag { get; set; }

    public string UnlinkedIgnoredRootDir { get; set; } = string.Empty;
    
    public List<string> UnlinkedCategories { get; set; } = [];

    public List<string> IgnoredDownloads { get; set; } = [];
}

public class CleanCategoryDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Max ratio before removing a download.
    /// </summary>
    public double MaxRatio { get; set; } = -1;

    /// <summary>
    /// Min number of hours to seed before removing a download, if the ratio has been met.
    /// </summary>
    public double MinSeedTime { get; set; }

    /// <summary>
    /// Number of hours to seed before removing a download.
    /// </summary>
    public double MaxSeedTime { get; set; } = -1;
} 