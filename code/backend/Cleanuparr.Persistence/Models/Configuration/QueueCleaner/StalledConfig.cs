using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Exceptions;

namespace Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

[ComplexType]
public sealed record StalledConfig
{
    public ushort MaxStrikes { get; init; }
    
    public bool ResetStrikesOnProgress { get; init; }
    
    public bool IgnorePrivate { get; init; }
    
    public bool DeletePrivate { get; init; }
    
    public ushort DownloadingMetadataMaxStrikes { get; init; }
    
    public void Validate()
    {
        if (MaxStrikes is > 0 and < 3)
        {
            throw new ValidationException("the minimum value for stalled max strikes must be 3");
        }
        
        if (DownloadingMetadataMaxStrikes is > 0 and < 3)
        {
            throw new ValidationException("the minimum value for downloading metadata max strikes must be 3");
        }
    }
}