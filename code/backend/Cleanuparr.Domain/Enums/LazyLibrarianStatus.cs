using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// The status of a LazyLibrarian wanted row.
/// </summary>
[JsonConverter(typeof(LazyLibrarianStatusConverter))]
public enum LazyLibrarianStatus
{
    Unknown = 0,
    Snatched,
    Seeding,
    Aborted,
    Failed,
    Processed,
    Have,
    Wanted,
    Skipped,
    Ignored,
}
