namespace Cleanuparr.Domain.Entities.Deluge.Response;

/// <summary>
/// A tracker of a torrent in Deluge.
/// </summary>
public sealed record Tracker
{
    /// <summary>
    /// The address of the tracker.
    /// </summary>
    public string Url { get; init; } = string.Empty;
}
