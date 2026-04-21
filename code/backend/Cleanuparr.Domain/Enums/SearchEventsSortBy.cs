using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Sorting fields available for search event listings.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SearchEventsSortBy
{
    /// <summary>Sort by the event timestamp.</summary>
    Timestamp,
    /// <summary>Sort by the item title associated with the search.</summary>
    Title,
    /// <summary>Sort by the search command status.</summary>
    Status,
    /// <summary>Sort by the search type (proactive, replacement, etc.).</summary>
    Type,
}
