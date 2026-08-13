using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Command state values reported by the arr command endpoint
/// </summary>
[JsonConverter(typeof(ArrCommandStateConverter))]
public enum ArrCommandState
{
    Unknown = 0,
    Queued,
    Started,
    Completed,
    Failed,
    Aborted,
    Cancelled,
    Orphaned,
}
