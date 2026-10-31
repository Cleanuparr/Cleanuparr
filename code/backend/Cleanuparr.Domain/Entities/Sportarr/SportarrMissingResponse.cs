namespace Cleanuparr.Domain.Entities.Sportarr;

/// <summary>
/// One page of Sportarr's native missing events list.
/// </summary>
public sealed record SportarrMissingResponse
{
    public IReadOnlyList<SportarrEvent> Events { get; init; } = [];

    public int TotalRecords { get; init; }
}
