using System.ComponentModel.DataAnnotations;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Models.QueueCleaner;

public abstract record QueueRuleDto
{
    public Guid? Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public bool Enabled { get; set; } = true;
    
    [Range(3, int.MaxValue, ErrorMessage = "Max strikes must be at least 3")]
    public int MaxStrikes { get; set; } = 3;
    
    public TorrentPrivacyType PrivacyType { get; set; } = TorrentPrivacyType.Public;
    
    [Range(1, 100, ErrorMessage = "Completion percentage must be between 1 and 100")]
    public double MaxCompletionPercentage { get; set; }
    
    public bool DeletePrivateTorrentsFromClient { get; set; } = false;
}
