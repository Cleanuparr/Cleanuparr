using System.Text.Json.Serialization;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Responses;

public sealed record UnlinkedConfigResponse
{
    public bool Enabled { get; init; }

    public required string TargetCategory { get; init; }

    public bool UseTag { get; init; }

    public required List<string> IgnoredRootDirs { get; init; }

    public required List<string> Categories { get; init; }

    public static UnlinkedConfigResponse From(UnlinkedConfig config) => new()
    {
        Enabled = config.Enabled,
        TargetCategory = config.TargetCategory,
        UseTag = config.UseTag,
        IgnoredRootDirs = config.IgnoredRootDirs,
        Categories = config.Categories,
    };
}
