namespace Cleanuparr.Domain.Entities.Arr.ManualImport;

/// <summary>
/// A piece of content an import candidate maps to, as the arr nests it: a series, an episode or a movie.
/// </summary>
public sealed record ManualImportRef
{
    public long Id { get; init; }
}
