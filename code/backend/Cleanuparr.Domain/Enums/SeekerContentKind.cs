using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// What a Seeker search unit holds, which decides how Sonarr searches it
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeekerContentKind
{
    /// <summary>
    /// A movie or a regular series season
    /// </summary>
    Standard,

    /// <summary>
    /// A season of a daily series
    /// </summary>
    Daily,

    /// <summary>
    /// A season or episode of an anime series
    /// </summary>
    Anime,

    /// <summary>
    /// An episode from season 0
    /// </summary>
    Special,
}
