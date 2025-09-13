using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed record NotificationConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(100)]
    public string Name { get; init; } = string.Empty;
    
    [Required]
    public NotificationProviderType Type { get; init; }
    
    public bool IsEnabled { get; init; } = true;
    
    public bool OnFailedImportStrike { get; init; }
    
    public bool OnStalledStrike { get; init; }
    
    public bool OnSlowStrike { get; init; }
    
    public bool OnQueueItemDeleted { get; init; }
    
    public bool OnDownloadCleaned { get; init; }
    
    public bool OnCategoryChanged { get; init; }
    
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
    
    public NotifiarrConfig? NotifiarrConfiguration { get; init; }
    
    public AppriseConfig? AppriseConfiguration { get; init; }
    
    public NtfyConfig? NtfyConfiguration { get; init; }
    
    [NotMapped]
    public bool IsConfigured => Type switch
    {
        NotificationProviderType.Notifiarr => NotifiarrConfiguration?.IsValid() == true,
        NotificationProviderType.Apprise => AppriseConfiguration?.IsValid() == true,
        NotificationProviderType.Ntfy => NtfyConfiguration?.IsValid() == true,
        _ => false
    };
    
    [NotMapped]
    public bool HasAnyEventEnabled => 
        OnFailedImportStrike ||
        OnStalledStrike ||
        OnSlowStrike ||
        OnQueueItemDeleted ||
        OnDownloadCleaned ||
        OnCategoryChanged;
}
