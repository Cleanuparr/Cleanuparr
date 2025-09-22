using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Exceptions;

namespace Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

[ComplexType]
public sealed record SlowConfig
{
    public ushort MaxStrikes { get; init; }
    
    public bool ResetStrikesOnProgress { get; init; }

    public bool IgnorePrivate { get; init; }
    
    public bool DeletePrivate { get; init; }

    public string MinSpeed { get; init; } = string.Empty;
    
    [JsonIgnore]
    public ByteSize MinSpeedByteSize => string.IsNullOrEmpty(MinSpeed) ? new ByteSize(0) : ByteSize.Parse(MinSpeed);
    
    public double MaxTime { get; init; }
    
    public string IgnoreAboveSize { get; init; } = string.Empty;
    
    [JsonIgnore]
    public ByteSize? IgnoreAboveSizeByteSize => string.IsNullOrEmpty(IgnoreAboveSize) ? null : ByteSize.Parse(IgnoreAboveSize);
    
    public void Validate()
    {
        if (MaxStrikes is > 0 and < 3)
        {
            throw new ValidationException("the minimum value for slow max strikes must be 3");
        }

        if (MaxStrikes > 0)
        {
            bool isSpeedSet = !string.IsNullOrEmpty(MinSpeed);

            if (isSpeedSet && ByteSize.TryParse(MinSpeed, out _) is false)
            {
                throw new ValidationException("invalid value for slow min speed");
            }

            if (MaxTime < 0)
            {
                throw new ValidationException("invalid value for slow max time");
            }

            if (!isSpeedSet && MaxTime is 0)
            {
                throw new ValidationException("either slow min speed or slow max time must be set");
            }
        
            bool isIgnoreAboveSizeSet = !string.IsNullOrEmpty(IgnoreAboveSize);
        
            if (isIgnoreAboveSizeSet && ByteSize.TryParse(IgnoreAboveSize, out _) is false)
            {
                throw new ValidationException($"invalid value for slow ignore above size: {IgnoreAboveSize}");
            }
        }
    }
}