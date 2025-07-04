using System.ComponentModel.DataAnnotations.Schema;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed record AppriseConfig : NotificationConfig
{
    public string? FullUrl { get; set; }
    
    [NotMapped]
    public Uri? Url => string.IsNullOrEmpty(FullUrl) ? null : new Uri(FullUrl, UriKind.Absolute);
    
    public string? Key { get; set; }
    
    public string? Tags { get; set; }
    
    public override bool IsValid()
    {
        if (Url is null)
        {
            return false;
        }
        
        if (string.IsNullOrEmpty(Key?.Trim()))
        {
            return false;
        }

        return true;
    }
}