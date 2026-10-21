namespace Cleanuparr.Domain.Enums;

public enum SonarrSearchMode
{
    /// <summary>
    /// Searches a whole season when it has finished airing and every aired monitored episode needs a search.
    /// Otherwise searches one episode per run.
    /// </summary>
    Auto,

    /// <summary>
    /// Always searches the whole season.
    /// </summary>
    Season,

    /// <summary>
    /// Always searches one episode per run.
    /// </summary>
    Episode,
}
