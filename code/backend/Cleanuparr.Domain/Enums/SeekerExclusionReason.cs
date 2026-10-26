using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Why Seeker leaves an item or unit out of the current search cycle
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeekerExclusionReason
{
    /// <summary>
    /// The movie or series is not released, or the episode has no air date yet
    /// </summary>
    NotReleased,

    /// <summary>
    /// Released less than the post-release grace hours ago
    /// </summary>
    InGracePeriod,

    /// <summary>
    /// Unmonitored while the instance searches monitored items only
    /// </summary>
    NotMonitored,

    /// <summary>
    /// Carries a skip tag
    /// </summary>
    SkipTag,

    /// <summary>
    /// Carries none of the include tags
    /// </summary>
    MissingIncludeTag,

    /// <summary>
    /// Not missing and not below any enabled cutoff
    /// </summary>
    NothingToSearch,

    /// <summary>
    /// Below the custom format score cutoff, but its quality profile disallows upgrades
    /// </summary>
    UpgradesDisabled,

    /// <summary>
    /// Downloading or importing in the arr queue
    /// </summary>
    InQueue,

    /// <summary>
    /// Grabbed within the recent grab window
    /// </summary>
    RecentlyGrabbed,

    /// <summary>
    /// Rests after repeated searches in a row grabbed nothing
    /// </summary>
    Resting,

    /// <summary>
    /// Already searched in the current cycle
    /// </summary>
    SearchedThisCycle,

    /// <summary>
    /// Radarr does not count the movie as available yet
    /// </summary>
    NotAvailable,
}
