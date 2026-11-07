namespace Cleanuparr.Domain.Entities.Sportarr;

/// <summary>
/// One page of Sportarr's history.
/// </summary>
public sealed record SportarrHistoryResponse
{
    public IReadOnlyList<SportarrHistoryRecord> Records { get; init; } = [];
}
