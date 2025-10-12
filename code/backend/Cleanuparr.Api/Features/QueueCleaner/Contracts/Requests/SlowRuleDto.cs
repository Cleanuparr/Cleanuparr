using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.QueueCleaner.Contracts.Requests;

public sealed record SlowRuleDto : QueueRuleDto
{
    public bool ResetStrikesOnProgress { get; set; } = true;
    
    public string MinSpeed { get; set; } = string.Empty;
    
    [Range(0, double.MaxValue, ErrorMessage = "Maximum time cannot be negative")]
    public double MaxTimeHours { get; set; } = 0;
    
    public string? IgnoreAboveSize { get; set; }
}
