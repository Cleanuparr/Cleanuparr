using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record SeekerPreviewUnitResponse
{
    public string Name { get; init; } = string.Empty;
    public SeekerSearchReason Reason { get; init; }

    /// <summary>
    /// Null for a movie.
    /// </summary>
    public SeekerContentKind? ContentKind { get; init; }
}
