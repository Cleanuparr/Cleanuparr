using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Task states Sportarr reports.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<SportarrTaskStatus>))]
public enum SportarrTaskStatus
{
    Unknown = 0,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
    Aborting,
}
