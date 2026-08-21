using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Whether LazyLibrarian created the download task itself.
/// It refuses to remove a task it adopted.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<LazyLibrarianOrigin>))]
public enum LazyLibrarianOrigin
{
    Unknown = 0,
    New,
    Adopted,
}
