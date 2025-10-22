namespace Cleanuparr.Api.Features.QueueCleaner.Contracts.Requests;

public sealed record StallRuleDto : QueueRuleDto
{
    public bool ResetStrikesOnProgress { get; set; } = true;
    
    public string? MinimumProgress { get; set; }
}
