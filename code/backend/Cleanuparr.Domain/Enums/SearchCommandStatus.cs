using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SearchCommandStatus
{
    Pending,
    Started,
    Completed,
    Failed,
    TimedOut
}
