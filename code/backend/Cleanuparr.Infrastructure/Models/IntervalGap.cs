using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Models;

public class IntervalGap
{
    public TorrentPrivacyType PrivacyType { get; set; }
    public double Start { get; set; }
    public double End { get; set; }
    
    public string GetPrivacyTypeDisplayName()
    {
        return PrivacyType switch
        {
            TorrentPrivacyType.Public => "Public",
            TorrentPrivacyType.Private => "Private",
            TorrentPrivacyType.Both => "Both",
            _ => "Unknown"
        };
    }
}
