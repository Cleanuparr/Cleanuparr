using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Filters custom format score rows by their relation to the configured quality cutoff.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CutoffFilter
{
    /// <summary>Include all items regardless of cutoff status.</summary>
    All,
    /// <summary>Include only items whose current score is below the cutoff.</summary>
    Below,
    /// <summary>Include only items whose current score meets or exceeds the cutoff.</summary>
    Met,
}
