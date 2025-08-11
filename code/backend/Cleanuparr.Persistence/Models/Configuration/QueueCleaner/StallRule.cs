using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Exceptions;

namespace Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

public sealed record StallRule : QueueRule
{
    public bool ResetStrikesOnProgress { get; init; } = true;
    
    public override bool MatchesTorrent(ITorrentInfo torrent)
    {
        // Check privacy type
        if (!MatchesPrivacyType(torrent.IsPrivate))
        {
            return false;
        }
            
        // Check completion percentage
        if (!MatchesCompletionPercentage(torrent.CompletionPercentage))
        {
            return false;
        }
            
        return true;
    }
    
    public override void Validate()
    {
        base.Validate();

        if (MaxStrikes < 3)
        {
            throw new ValidationException("Stall rule max strikes must be at least 3");
        }
    }
}