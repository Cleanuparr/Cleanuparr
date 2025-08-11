using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Models;

public class RuleInterval
{
    public TorrentPrivacyType PrivacyType { get; set; }
    public double Start { get; set; } = 0;
    public double End { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public Guid RuleId { get; set; }
}
