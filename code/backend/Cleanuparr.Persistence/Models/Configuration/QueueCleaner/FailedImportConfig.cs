using System;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;

namespace Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

[ComplexType]
public sealed record FailedImportConfig
{
    public ushort MaxStrikes { get; init; }

    public bool IgnorePrivate { get; init; }

    public bool DeletePrivate { get; init; }

    public bool SkipIfNotFoundInClient { get; init; } = true;

    public IReadOnlyList<string> Patterns { get; init; } = [];

    public PatternMode PatternMode { get; init; } = PatternMode.Include;
    
    public void Validate()
    {
        if (MaxStrikes is > 0 and < 3)
        {
            throw new ValidationException("The minimum value for failed imports max strikes must be 3");
        }

        if (MaxStrikes >= 3 && PatternMode is PatternMode.Include && Patterns.Count is 0)
        {
            throw new ValidationException("At least one pattern must be specified when using the Include pattern mode");
        }
    }
}