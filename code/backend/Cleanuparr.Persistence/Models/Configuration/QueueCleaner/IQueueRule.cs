using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

public interface IQueueRule
{
    Guid Id { get; }
    
    string Name { get; }
    
    bool Enabled { get; }
    
    int MaxStrikes { get; }
    
    TorrentPrivacyType PrivacyType { get; }
    
    double MaxCompletionPercentage { get; }
    
    bool MatchesTorrent(ITorrentInfo torrent);
    
    void Validate();
}