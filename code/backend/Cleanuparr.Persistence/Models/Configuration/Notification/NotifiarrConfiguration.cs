using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Persistence.Models.Configuration;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed record NotifiarrConfiguration : IConfig
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
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new ValidationException("Notifiarr API key is required");
        }
        
        if (ApiKey.Length < 10)
        {
            throw new ValidationException("Notifiarr API key must be at least 10 characters long");
        }
        
        if (string.IsNullOrWhiteSpace(ChannelId))
        {
            throw new ValidationException("Discord channel ID is required");
        }
        
        if (!ulong.TryParse(ChannelId, out _))
        {
            throw new ValidationException("Discord channel ID must be a valid numeric ID");
        }
    }
}
