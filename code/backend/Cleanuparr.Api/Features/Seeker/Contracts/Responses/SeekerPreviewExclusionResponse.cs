using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record SeekerPreviewExclusionResponse
{
    public string Name { get; init; } = string.Empty;
    public SeekerExclusionReason Reason { get; init; }
}
