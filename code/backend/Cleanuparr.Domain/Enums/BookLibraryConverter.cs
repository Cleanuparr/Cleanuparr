using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Maps a LazyLibrarian wire value to <see cref="BookLibrary"/>.
/// Any value the enum does not name becomes <see cref="BookLibrary.Unknown"/>.
/// A magazine issue date contains digits, so it lands on Unknown.
/// </summary>
public sealed class BookLibraryConverter : JsonConverter<BookLibrary>
{
    public override bool HandleNull => true;

    public override BookLibrary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return BookLibrary.Unknown;
        }

        string? raw = reader.GetString();

        return raw is not null
               && raw.All(char.IsAsciiLetter)
               && Enum.TryParse(raw, ignoreCase: true, out BookLibrary parsed)
            ? parsed
            : BookLibrary.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, BookLibrary value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
