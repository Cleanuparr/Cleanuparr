using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

/// <summary>
/// An item left out of the search, with the first filter that dropped it.
/// </summary>
public sealed record SeekerPreviewExclusionResponse
{
    public string Name { get; init; } = string.Empty;
    public SeekerExclusionReason Reason { get; init; }

    /// <summary>
    /// Set while the reason is <see cref="SeekerExclusionReason.Resting"/>, as are the other rest fields.
    /// </summary>
    public int? ConsecutiveMisses { get; init; }

    /// <summary>
    /// Cycle ends before the item is searched again.
    /// </summary>
    public int? RestCyclesLeft { get; init; }

    /// <summary>
    /// When the rest ends, however many cycles it has left.
    /// </summary>
    public DateTimeOffset? RestUntil { get; init; }
}
