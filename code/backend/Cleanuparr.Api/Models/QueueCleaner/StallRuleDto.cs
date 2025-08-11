namespace Cleanuparr.Api.Models.QueueCleaner;

public sealed record StallRuleDto : QueueRuleDto
{
    public bool ResetStrikesOnProgress { get; set; } = true;
}