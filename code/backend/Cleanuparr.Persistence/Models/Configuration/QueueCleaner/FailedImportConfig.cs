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

    public bool ChangeCategory { get; init; }

    /// <summary>
    /// Whether to import a blocked download instead of striking it, when every reason the arr gave is safe to force past.
    /// </summary>
    public bool ForceImport { get; init; }

    /// <summary>
    /// How many times to ask the arr to import one download before giving up on it.
    /// </summary>
    public ushort ForceImportMaxTries { get; init; } = 3;

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

        if (ForceImport && ForceImportMaxTries is 0)
        {
            throw new ValidationException("Force import max tries must be at least 1");
        }

        if (ChangeCategory && DeletePrivate)
        {
            throw new ValidationException("Cannot enable both deletion and category changing");
        }
    }
}