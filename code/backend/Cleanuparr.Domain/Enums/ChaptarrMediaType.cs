using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Which side of a title a Chaptarr book row covers.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<ChaptarrMediaType>))]
public enum ChaptarrMediaType
{
    Unknown = 0,
    Audiobook,
    Ebook,
}
