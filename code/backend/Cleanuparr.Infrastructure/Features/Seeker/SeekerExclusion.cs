using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// A movie, series, season or episode Seeker leaves out, with the first stage that dropped it.
/// </summary>
/// <param name="Rest">Set when the reason is <see cref="SeekerExclusionReason.Resting"/>.</param>
public sealed record SeekerExclusion(string Name, SeekerExclusionReason Reason, SeekerUnitRest? Rest = null);
