using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// What a seeding rule does to a download that matched it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeedingRuleAction
{
    /// <summary>
    /// Remove the download from the client.
    /// </summary>
    Delete,

    /// <summary>
    /// Stop the download in the client and leave it there.
    /// </summary>
    Stop,

    /// <summary>
    /// Text this build does not know.
    /// </summary>
    Unknown = EnumSentinel.UnknownValue,
}
