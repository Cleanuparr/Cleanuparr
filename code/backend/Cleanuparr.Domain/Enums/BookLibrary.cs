using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// The LazyLibrarian library a wanted row belongs to, read from its AuxInfo column.
/// A magazine row carries an issue date there, so it reads as Unknown.
/// </summary>
[JsonConverter(typeof(BookLibraryConverter))]
public enum BookLibrary
{
    Unknown = 0,
    EBook,
    AudioBook,
    Comic,
}
