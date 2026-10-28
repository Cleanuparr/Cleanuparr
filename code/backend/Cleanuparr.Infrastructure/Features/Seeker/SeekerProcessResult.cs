using Cleanuparr.Persistence.Models.State;

namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// Result of processing an arr instance for proactive search candidates.
/// </summary>
internal sealed record SeekerProcessResult
{
    public required List<SeekerSearchCandidate> Candidates { get; init; }

    /// <summary>
    /// Cycle the candidates belong to.
    /// </summary>
    public required SeekerCycle Cycle { get; init; }

    public required SeekerEvaluation Evaluation { get; init; }
}
