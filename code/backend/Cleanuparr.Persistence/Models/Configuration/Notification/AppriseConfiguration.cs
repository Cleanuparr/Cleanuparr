using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed record AppriseConfiguration
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
}
