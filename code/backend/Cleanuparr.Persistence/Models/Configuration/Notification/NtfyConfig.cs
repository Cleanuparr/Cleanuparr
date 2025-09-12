using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed record NtfyConfig : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    [Required]
    public Guid NotificationConfigId { get; init; }
    
    public NotificationConfig NotificationConfig { get; init; } = null!;
    
    [Required]
    [MaxLength(500)]
    public string ServerUrl { get; init; } = string.Empty;
    
    [Required]
    public List<string> Topics { get; init; } = new();
    
    [Required]
    public NtfyAuthenticationType AuthenticationType { get; init; } = NtfyAuthenticationType.None;
    
    [MaxLength(255)]
    public string? Username { get; init; }
    
    [MaxLength(255)]
    public string? Password { get; init; }
    
    [MaxLength(500)]
    public string? AccessToken { get; init; }
    
    [Required]
    public NtfyPriority Priority { get; init; } = NtfyPriority.Default;
    
    public List<string> Tags { get; init; } = new();
    
    [NotMapped]
    public Uri? Uri
    {
        get
        {
            try
            {
                return string.IsNullOrWhiteSpace(ServerUrl) ? null : new Uri(ServerUrl, UriKind.Absolute);
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
               Topics.Any(t => !string.IsNullOrWhiteSpace(t)) &&
               IsAuthenticationValid();
    }
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            throw new ValidationException("ntfy server URL is required");
        }
        
        if (Uri == null)
        {
            throw new ValidationException("ntfy server URL must be a valid HTTP or HTTPS URL");
        }
        
        if (!Topics.Any(t => !string.IsNullOrWhiteSpace(t)))
        {
            throw new ValidationException("At least one ntfy topic is required");
        }
        
        ValidateAuthentication();
    }
    
    private bool IsAuthenticationValid()
    {
        return AuthenticationType switch
        {
            NtfyAuthenticationType.None => true,
            NtfyAuthenticationType.BasicAuth => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password),
            NtfyAuthenticationType.AccessToken => !string.IsNullOrWhiteSpace(AccessToken),
            _ => false
        };
    }
    
    private void ValidateAuthentication()
    {
        switch (AuthenticationType)
        {
            case NtfyAuthenticationType.BasicAuth:
                if (string.IsNullOrWhiteSpace(Username))
                {
                    throw new ValidationException("Username is required for Basic Auth");
                }
                if (string.IsNullOrWhiteSpace(Password))
                {
                    throw new ValidationException("Password is required for Basic Auth");
                }
                break;
                
            case NtfyAuthenticationType.AccessToken:
                if (string.IsNullOrWhiteSpace(AccessToken))
                {
                    throw new ValidationException("Access token is required for Token authentication");
                }
                break;
        }
    }
}
