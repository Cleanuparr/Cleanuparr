namespace Cleanuparr.Domain.Entities.Arr.History;

/// <summary>
/// The episode data embedded in an *arr history row.
/// </summary>
public sealed record ArrHistoryEpisode
{
    /// <summary>
    /// The season number the episode belongs to.
    /// </summary>
    public long SeasonNumber { get; init; }
}
