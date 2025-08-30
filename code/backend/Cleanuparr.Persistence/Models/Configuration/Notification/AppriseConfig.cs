using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Persistence.Models.Configuration;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed record AppriseConfig : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    [Required]
    public Guid NotificationConfigId { get; init; }
    
    public NotificationConfig NotificationConfig { get; init; } = null!;
    
    [Required]
    [MaxLength(500)]
    public string Url { get; init; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string Key { get; init; } = string.Empty;
    
    [MaxLength(255)]
    public string? Tags { get; init; }
    
    [NotMapped]
    public Uri? Uri
    {
        get
        {
            try
            {
                return string.IsNullOrWhiteSpace(Url) ? null : new Uri(Url, UriKind.Absolute);
            }
            catch
            {
                return null;
            }
        }
    }
    
    public bool IsValid()
    {
        return Uri != null && 
               !string.IsNullOrWhiteSpace(Key);
    }
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            throw new ValidationException("Apprise server URL is required");
        }
        
        if (Uri == null)
        {
            throw new ValidationException("Apprise server URL must be a valid HTTP or HTTPS URL");
        }
        
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new ValidationException("Apprise configuration key is required");
        }
        
        if (Key.Length < 2)
        {
            throw new ValidationException("Apprise configuration key must be at least 2 characters long");
        }
    }
}
