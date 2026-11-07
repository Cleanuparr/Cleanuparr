namespace Cleanuparr.Domain.Entities.Sportarr;

/// <summary>
/// One row of Sportarr's history.
/// </summary>
public sealed record SportarrHistoryRecord
{
    public long EpisodeId { get; init; }

    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// Sportarr can send the UTC time without an offset.
    /// </summary>
    public string Date { get; init; } = string.Empty;
}
