using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed record NotifiarrConfiguration
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    [Required]
    public Guid ProviderId { get; init; }
    
    [Required]
    [MaxLength(255)]
    public string ApiKey { get; init; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string ChannelId { get; init; } = string.Empty;
    
    // Navigation property
    [ForeignKey(nameof(ProviderId))]
    public NotificationProvider Provider { get; init; } = null!;
    
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(ApiKey) && 
               !string.IsNullOrWhiteSpace(ChannelId);
    }
}
