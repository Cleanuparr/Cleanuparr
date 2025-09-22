using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Exceptions;

namespace Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

[ComplexType]
public sealed record FailedImportConfig
{
    public ushort MaxStrikes { get; init; }
    
    public bool IgnorePrivate { get; init; }
    
    public bool DeletePrivate { get; init; }

    public IReadOnlyList<string> IgnoredPatterns { get; init; } = [];
    
    public void Validate()
    {
        if (MaxStrikes is > 0 and < 3)
        {
            throw new ValidationException("the minimum value for failed imports max strikes must be 3");
        }
    }
}