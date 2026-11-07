using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// A movie, series, season or episode Seeker leaves out, with the first stage that dropped it.
/// </summary>
public sealed record SeekerExclusion(string Name, SeekerExclusionReason Reason);
