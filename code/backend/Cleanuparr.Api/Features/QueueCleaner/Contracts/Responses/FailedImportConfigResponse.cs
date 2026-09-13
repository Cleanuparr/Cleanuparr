using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

namespace Cleanuparr.Api.Features.QueueCleaner.Contracts.Responses;

public sealed record FailedImportConfigResponse
{
    public ushort MaxStrikes { get; init; }

    public bool IgnorePrivate { get; init; }

    public bool DeletePrivate { get; init; }

    public bool SkipIfNotFoundInClient { get; init; }

    public required IReadOnlyList<string> Patterns { get; init; }

    public PatternMode PatternMode { get; init; }

    public bool ChangeCategory { get; init; }

    public static FailedImportConfigResponse From(FailedImportConfig config) => new()
    {
        MaxStrikes = config.MaxStrikes,
        IgnorePrivate = config.IgnorePrivate,
        DeletePrivate = config.DeletePrivate,
        SkipIfNotFoundInClient = config.SkipIfNotFoundInClient,
        Patterns = config.Patterns,
        PatternMode = config.PatternMode,
        ChangeCategory = config.ChangeCategory,
    };
}
