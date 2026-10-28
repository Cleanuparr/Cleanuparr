using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Result of the latest search command sent for a Seeker unit
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeekerSearchOutcome
{
    /// <summary>
    /// The search added a matching release to the arr queue
    /// </summary>
    Grabbed,

    /// <summary>
    /// The command completed without queueing a release
    /// </summary>
    NothingGrabbed,

    /// <summary>
    /// The command failed or timed out
    /// </summary>
    Failed,
}
