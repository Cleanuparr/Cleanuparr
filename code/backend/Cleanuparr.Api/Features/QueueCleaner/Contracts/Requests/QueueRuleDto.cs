using System.ComponentModel.DataAnnotations;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.QueueCleaner.Contracts.Requests;

public abstract record QueueRuleDto
{
    public Guid? Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public bool Enabled { get; set; } = true;
    
    [Range(3, int.MaxValue, ErrorMessage = "Max strikes must be at least 3")]
    public int MaxStrikes { get; set; } = 3;
    
    public TorrentPrivacyType PrivacyType { get; set; } = TorrentPrivacyType.Public;
    
    [Range(0, 100, ErrorMessage = "Minimum completion percentage must be between 0 and 100")]
    public ushort MinCompletionPercentage { get; set; }
    
    [Range(0, 100, ErrorMessage = "Maximum completion percentage must be between 0 and 100")]
    public ushort MaxCompletionPercentage { get; set; }
    
    public bool DeletePrivateTorrentsFromClient { get; set; } = false;
}
