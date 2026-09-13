using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

namespace Cleanuparr.Api.Features.QueueCleaner.Contracts.Responses;

public sealed record StallRuleResponse : QueueRuleResponse
{
    public bool ResetStrikesOnProgress { get; init; }

    public string? MinimumProgress { get; init; }

    public static StallRuleResponse From(StallRule rule) => new()
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
        MinimumProgress = rule.MinimumProgress,
    };
}
