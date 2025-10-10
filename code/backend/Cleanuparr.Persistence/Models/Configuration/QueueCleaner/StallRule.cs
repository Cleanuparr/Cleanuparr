using System.Text.Json.Serialization;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Exceptions;

namespace Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

public sealed record StallRule : QueueRule
{
    public bool ResetStrikesOnProgress { get; init; } = true;
    public string? MinimumProgress { get; init; }

    [JsonIgnore]
    public ByteSize? MinimumProgressByteSize => string.IsNullOrWhiteSpace(MinimumProgress)
        ? null
        : ByteSize.Parse(MinimumProgress);
    
    public override bool MatchesTorrent(ITorrentItem torrent)
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

        if (!string.IsNullOrWhiteSpace(MinimumProgress))
        {
            if (!ByteSize.TryParse(MinimumProgress, out var parsed))
            {
                throw new ValidationException($"Invalid minimum progress value: {MinimumProgress}");
            }

            if (parsed.HasValue && parsed.Value.Bytes < 0)
            {
                throw new ValidationException("Minimum progress must be zero or greater");
            }
        }
    }
}