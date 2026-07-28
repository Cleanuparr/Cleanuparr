namespace Cleanuparr.Domain.Entities.Radarr;

/// <summary>
/// A movie in Radarr or Whisparr v3.
/// </summary>
public sealed record Movie
{
    /// <summary>
    /// The ID of the movie.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The name of the movie.
    /// </summary>
    public string Title { get; init; } = string.Empty;
}
