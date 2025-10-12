using System.Text.Json.Serialization;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Exceptions;

namespace Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

public sealed record SlowRule : QueueRule
{
    public bool ResetStrikesOnProgress { get; init; } = true;
    public double MaxTimeHours { get; init; } = 0;
    
    public string MinSpeed { get; init; } = string.Empty;
    
    [JsonIgnore]
    public ByteSize MinSpeedByteSize => string.IsNullOrEmpty(MinSpeed) ? new ByteSize(0) : ByteSize.Parse(MinSpeed);
    
    public string? IgnoreAboveSize { get; init; } = string.Empty;
    
    [JsonIgnore]
    public ByteSize? IgnoreAboveSizeByteSize => string.IsNullOrEmpty(IgnoreAboveSize) ? null : ByteSize.Parse(IgnoreAboveSize);

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
            
        // Check file size
        if (!MatchesFileSize(torrent.Size))
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
            throw new ValidationException("Slow rule max strikes must be at least 3");
        }
            
        if (MaxTimeHours < 0)
        {
            throw new ValidationException("Maximum time cannot be negative");
        }
            
        bool hasMinSpeed = !string.IsNullOrEmpty(MinSpeed);
        bool hasMaxTime = MaxTimeHours > 0;

        if (!hasMinSpeed && !hasMaxTime)
        {
            throw new ValidationException("Either minimum speed or maximum time must be specified");
        }

        if (hasMinSpeed && !ByteSize.TryParse(MinSpeed, out _))
        {
            throw new ValidationException("Invalid minimum speed format");
        }

        bool isIgnoreAboveSizeSet = !string.IsNullOrEmpty(IgnoreAboveSize);
        
        if (isIgnoreAboveSizeSet && ByteSize.TryParse(IgnoreAboveSize, out _) is false)
        {
            throw new ValidationException($"invalid value for slow ignore above size: {IgnoreAboveSize}");
        }
    }
    
    private bool MatchesFileSize(long size)
    {
        if (size >= IgnoreAboveSizeByteSize?.Bytes)
        {
            return false;
        }
            
        return true;
    }
}