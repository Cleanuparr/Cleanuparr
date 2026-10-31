namespace Cleanuparr.Domain.Entities.Arr.Queue;

/// <summary>
/// Episode data attached to a Sonarr or Whisparr v2 queue record.
/// </summary>
public sealed record QueueEpisode
{
    /// <summary>
    /// True when the episode has a file, which makes its download an upgrade.
    /// </summary>
    public bool HasFile { get; init; }
}
