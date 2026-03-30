namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;

public sealed record UpdateDownloadCleanerConfigRequest
{
    public bool Enabled { get; init; }

    public string CronExpression { get; init; } = "0 0 * * * ?";

    /// <summary>
    /// Indicates whether to use the CronExpression directly or convert from a user-friendly schedule.
    /// </summary>
    public bool UseAdvancedScheduling { get; init; }

    public List<string> IgnoredDownloads { get; init; } = [];
}
