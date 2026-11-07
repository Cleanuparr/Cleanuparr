namespace Cleanuparr.Domain.Entities.Sportarr;

/// <summary>
/// An event row from Sportarr's native wanted list.
/// </summary>
public sealed record SportarrEvent
{
    public long Id { get; init; }

    public string? Title { get; init; }

    public long LeagueId { get; init; }

    public int? SeasonNumber { get; init; }

    public int? EpisodeNumber { get; init; }

    /// <summary>
    /// UTC instant, usually without an offset.
    /// Null when the event has no date.
    /// </summary>
    public string? EventDate { get; init; }

    public bool Monitored { get; init; }

    public bool HasFile { get; init; }

    public string? Status { get; init; }
}
