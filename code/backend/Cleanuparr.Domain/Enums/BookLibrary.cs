using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// The LazyLibrarian library a wanted row belongs to, read from its AuxInfo column.
/// A magazine issue date or a comic issue key reads as Unknown.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<BookLibrary>))]
public enum BookLibrary
{
    Unknown = 0,
    EBook,
    AudioBook,
    Comic,
}
