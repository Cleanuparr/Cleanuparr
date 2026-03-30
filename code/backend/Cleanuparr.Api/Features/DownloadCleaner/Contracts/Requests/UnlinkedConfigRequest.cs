namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;

public sealed record UnlinkedConfigRequest
{
    public bool Enabled { get; init; }

    public string TargetCategory { get; init; } = "cleanuparr-unlinked";

    public bool UseTag { get; init; }

    public List<string> IgnoredRootDirs { get; init; } = [];

    public List<string> Categories { get; init; } = [];

    public string? DownloadDirectorySource { get; init; }

    public string? DownloadDirectoryTarget { get; init; }
}
