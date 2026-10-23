namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// A unit skipped after repeated searches in a row grabbed nothing.
/// </summary>
/// <param name="ConsecutiveMisses">Searches in a row that grabbed nothing.</param>
/// <param name="CyclesLeft">Cycle ends before the unit is searched again.</param>
/// <param name="Until">When the rest ends, however many cycles it has left.</param>
public sealed record SeekerUnitRest(int ConsecutiveMisses, int CyclesLeft, DateTimeOffset Until);
