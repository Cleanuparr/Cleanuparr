using System.Text.Json.Serialization;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Responses;

public sealed record SeedingRuleResponse
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public required List<string> Categories { get; init; }

    public required List<string> TrackerPatterns { get; init; }

    public required List<string> TagsAny { get; init; }

    public required List<string> TagsAll { get; init; }

    public int Priority { get; init; }

    public TorrentPrivacyType PrivacyType { get; init; }

    public double MaxRatio { get; init; }

    public double MinSeedTime { get; init; }

    public double MaxSeedTime { get; init; }

    public int? MinSeeders { get; init; }

    public bool DeleteSourceFiles { get; init; }

    public static SeedingRuleResponse From(ISeedingRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        Categories = rule.Categories,
        TrackerPatterns = rule.TrackerPatterns,
        TagsAny = (rule as ITagFilterable)?.TagsAny ?? [],
        TagsAll = (rule as ITagFilterable)?.TagsAll ?? [],
        Priority = rule.Priority,
        PrivacyType = rule.PrivacyType,
        MaxRatio = rule.MaxRatio,
        MinSeedTime = rule.MinSeedTime,
        MaxSeedTime = rule.MaxSeedTime,
        MinSeeders = (rule as ISeedersFilterable)?.MinSeeders,
        DeleteSourceFiles = rule.DeleteSourceFiles,
    };
}
