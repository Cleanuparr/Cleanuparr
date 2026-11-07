namespace Cleanuparr.Domain.Entities.Arr.History;

/// <summary>
/// One row of an *arr application's history.
/// </summary>
public sealed record ArrHistoryRecord
{
    /// <summary>
    /// The movie this row is about. 0 when the row is not about a movie.
    /// </summary>
    public long MovieId { get; init; }

    /// <summary>
    /// The series this row is about. 0 when the row is not about a series.
    /// </summary>
    public long SeriesId { get; init; }

    /// <summary>
    /// The episode this row is about. 0 when the row is not about an episode.
    /// </summary>
    public long EpisodeId { get; init; }

    /// <summary>
    /// The episode's own data, present only when the query asked for it.
    /// </summary>
    public ArrHistoryEpisode? Episode { get; init; }
}
