using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Domain.Entities.Sportarr;

/// <summary>
/// A task row from Sportarr's task list.
/// </summary>
public sealed record SportarrTask
{
    public long Id { get; init; }

    public SportarrTaskStatus Status { get; init; }

    /// <summary>
    /// Set only when the task hit a real error.
    /// </summary>
    public string? Exception { get; init; }
}
