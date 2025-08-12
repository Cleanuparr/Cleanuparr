using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

public abstract record QueueRule : IConfig, IQueueRule
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public Guid QueueCleanerConfigId { get; set; }
    
    public QueueCleanerConfig QueueCleanerConfig { get; set; } = null!;
    
    public required string Name { get; init; }
    
    public bool Enabled { get; init; } = true;
    
    public int MaxStrikes { get; init; } = 3;
    
    public TorrentPrivacyType PrivacyType { get; init; } = TorrentPrivacyType.Public;
    
    public double MaxCompletionPercentage { get; init; }
    
    public bool DeletePrivateTorrentsFromClient { get; init; } = false;
    
    public abstract bool MatchesTorrent(ITorrentInfo torrent);
    
    public virtual void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new Cleanuparr.Domain.Exceptions.ValidationException("Rule name cannot be empty");
        }

        if (MaxStrikes < 3)
        {
            throw new Cleanuparr.Domain.Exceptions.ValidationException("Max strikes must be at least 3");
        }

        if (MaxCompletionPercentage is <= 0 or > 100)
        {
            throw new Cleanuparr.Domain.Exceptions.ValidationException("Completion percentage must be between 1 and 100");
        }
    }
    
    protected bool MatchesPrivacyType(bool isPrivate)
    {
        return PrivacyType switch
        {
            TorrentPrivacyType.Public => !isPrivate,
            TorrentPrivacyType.Private => isPrivate,
            TorrentPrivacyType.Both => true,
            _ => true
        };
    }
    
    protected bool MatchesCompletionPercentage(double completionPercentage)
    {
        return completionPercentage <= MaxCompletionPercentage;
    }
}