namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;

public sealed record DeadTorrentConfigRequest
{
    public bool Enabled { get; init; }

    public string TargetCategory { get; init; } = "cleanuparr-dead";

    public bool UseTag { get; init; }

    public int MaxStrikes { get; init; }

    public List<string> Categories { get; init; } = [];
}
