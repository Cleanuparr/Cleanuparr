using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Responses;

public sealed record DeadTorrentConfigResponse
{
    public bool Enabled { get; init; }

    public required string TargetCategory { get; init; }

    public bool UseTag { get; init; }

    public int MaxStrikes { get; init; }

    public required List<string> Categories { get; init; }

    public static DeadTorrentConfigResponse From(DeadTorrentConfig config) => new()
    {
        Enabled = config.Enabled,
        TargetCategory = config.TargetCategory,
        UseTag = config.UseTag,
        MaxStrikes = config.MaxStrikes,
        Categories = config.Categories,
    };
}
