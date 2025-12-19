namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;

public sealed record UpdateDownloadCleanerConfigRequest
{
    public bool Enabled { get; init; }

    public string CronExpression { get; init; } = "0 0 * * * ?";

    /// <summary>
    /// Indicates whether to use the CronExpression directly or convert from a user-friendly schedule.
    /// </summary>
    public bool UseAdvancedScheduling { get; init; }

    public List<SeedingRuleRequest> Categories { get; init; } = [];

    public bool DeletePrivate { get; init; }
    
    /// <summary>
    /// Indicates whether unlinked download handling is enabled.
    /// </summary>
    public bool UnlinkedEnabled { get; init; }
    
    public string UnlinkedTargetCategory { get; init; } = "cleanuparr-unlinked";

    public bool UnlinkedUseTag { get; init; }

    public List<string> UnlinkedIgnoredRootDirs { get; init; } = [];
    
    public List<string> UnlinkedCategories { get; init; } = [];

    public List<string> IgnoredDownloads { get; init; } = [];
}
