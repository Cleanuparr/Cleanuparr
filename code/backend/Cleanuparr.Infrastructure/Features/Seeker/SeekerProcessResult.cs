namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// Result of processing an arr instance for proactive search candidates.
/// </summary>
internal sealed record SeekerProcessResult
{
    public required List<SeekerSearchCandidate> Candidates { get; init; }
    public required List<long> AllLibraryIds { get; init; }
}
