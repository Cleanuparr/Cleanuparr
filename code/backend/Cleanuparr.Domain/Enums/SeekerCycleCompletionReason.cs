using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Why a Seeker cycle ended
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeekerCycleCompletionReason
{
    /// <summary>
    /// Every eligible unit was searched
    /// </summary>
    Completed,
}
