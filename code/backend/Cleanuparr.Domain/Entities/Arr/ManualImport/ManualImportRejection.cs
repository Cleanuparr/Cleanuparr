namespace Cleanuparr.Domain.Entities.Arr.ManualImport;

/// <summary>
/// A reason an arr refuses to import a candidate file.
/// </summary>
public sealed record ManualImportRejection
{
    public string? Reason { get; init; }

    public string? Type { get; init; }
}
