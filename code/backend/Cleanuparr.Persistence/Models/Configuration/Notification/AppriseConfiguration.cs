using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Persistence.Models.Configuration;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed record AppriseConfiguration : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    [Required]
    public Guid ProviderId { get; init; }
    
    [Required]
    [MaxLength(500)]
    public string Url { get; init; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string Key { get; init; } = string.Empty;
    
    [MaxLength(255)]
    public string? Tags { get; init; }
    
    // Navigation property
    [ForeignKey(nameof(ProviderId))]
    public NotificationProvider Provider { get; init; } = null!;
    
    [NotMapped]
    public Uri? ParsedUrl
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
        return ParsedUrl != null && 
               !string.IsNullOrWhiteSpace(Key);
    }
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            throw new ValidationException("Apprise server URL is required");
        }
        
        if (ParsedUrl == null)
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
