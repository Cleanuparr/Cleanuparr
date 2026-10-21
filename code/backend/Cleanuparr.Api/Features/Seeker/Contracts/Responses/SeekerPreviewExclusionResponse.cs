using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

/// <summary>
/// An item left out of the search, with the first filter that dropped it.
/// </summary>
public sealed record SeekerPreviewExclusionResponse
{
    public string Name { get; init; } = string.Empty;
    public SeekerExclusionReason Reason { get; init; }
}
