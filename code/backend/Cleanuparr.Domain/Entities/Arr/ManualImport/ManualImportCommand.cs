namespace Cleanuparr.Domain.Entities.Arr.ManualImport;

/// <summary>
/// The command that makes an arr import files it refused to import on its own.
/// </summary>
public sealed record ManualImportCommand
{
    public string Name { get; init; } = "ManualImport";

    public List<ManualImportFile> Files { get; init; } = [];

    /// <summary>
    /// "auto" leaves the choice between a move, a copy and a hardlink to the arr's own setting.
    /// </summary>
    public string ImportMode { get; init; } = "auto";
}
