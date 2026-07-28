namespace Cleanuparr.Domain.Entities.Sonarr;

/// <summary>
/// A series in Sonarr or Whisparr v2.
/// </summary>
public sealed record Series
{
    /// <summary>
    /// The ID of the series.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The name of the series.
    /// </summary>
    public string Title { get; init; } = string.Empty;
}
