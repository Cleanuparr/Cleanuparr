using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// An episode, or a Lidarr album, that needs a search, with the reason it qualified.
/// </summary>
internal sealed record SonarrWantedEpisode
{
    public required long SeriesId { get; init; }

    public required int SeasonNumber { get; init; }

    public required long EpisodeId { get; init; }

    /// <summary>
    /// Null when the episode came from the custom format score cache, which stores no episode number.
    /// </summary>
    public int? EpisodeNumber { get; init; }

    /// <summary>
    /// The cached "Show S01E05" title, set when <see cref="EpisodeNumber"/> is null.
    /// </summary>
    public string? Title { get; init; }

    public DateTimeOffset? AirDateUtc { get; init; }

    public DateTimeOffset? LastSearchTime { get; init; }

    public required bool Monitored { get; init; }

    /// <summary>
    /// Cutoff minus current custom format score; 0 unless the reason is a custom format score below cutoff.
    /// </summary>
    public int CustomFormatGap { get; init; }

    public required SeekerSearchReason Reason { get; init; }
}
