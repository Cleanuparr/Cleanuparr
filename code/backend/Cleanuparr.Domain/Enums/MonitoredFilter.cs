using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Filters items by their monitored state in the source *arr application.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MonitoredFilter
{
    /// <summary>Include both monitored and unmonitored items.</summary>
    All,
    /// <summary>Include only monitored items.</summary>
    Monitored,
    /// <summary>Include only unmonitored items.</summary>
    Unmonitored,
}
