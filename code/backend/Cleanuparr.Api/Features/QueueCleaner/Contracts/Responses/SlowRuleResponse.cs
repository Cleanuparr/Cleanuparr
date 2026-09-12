using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

namespace Cleanuparr.Api.Features.QueueCleaner.Contracts.Responses;

public sealed record SlowRuleResponse : QueueRuleResponse
{
    public bool ResetStrikesOnProgress { get; init; }

    public double MaxTimeHours { get; init; }

    public bool IgnoreWhileAltSpeedActive { get; init; }

    public required string MinSpeed { get; init; }

    public string? IgnoreAboveSize { get; init; }

    public static SlowRuleResponse From(SlowRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        Enabled = rule.Enabled,
        MaxStrikes = rule.MaxStrikes,
        PrivacyType = rule.PrivacyType,
        MinCompletionPercentage = rule.MinCompletionPercentage,
        MaxCompletionPercentage = rule.MaxCompletionPercentage,
        DeletePrivateTorrentsFromClient = rule.DeletePrivateTorrentsFromClient,
        ChangeCategory = rule.ChangeCategory,
        ResetStrikesOnProgress = rule.ResetStrikesOnProgress,
        MaxTimeHours = rule.MaxTimeHours,
        IgnoreWhileAltSpeedActive = rule.IgnoreWhileAltSpeedActive,
        MinSpeed = rule.MinSpeed,
        IgnoreAboveSize = rule.IgnoreAboveSize,
    };
}
