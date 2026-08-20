using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Maps a LazyLibrarian wire value to <see cref="LazyLibrarianOrigin"/>.
/// Any value the enum does not name becomes <see cref="LazyLibrarianOrigin.Unknown"/>.
/// </summary>
public sealed class LazyLibrarianOriginConverter : JsonConverter<LazyLibrarianOrigin>
{
    public override bool HandleNull => true;

    public override LazyLibrarianOrigin Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return LazyLibrarianOrigin.Unknown;
        }

        string? raw = reader.GetString();

        return raw is not null
               && raw.All(char.IsAsciiLetter)
               && Enum.TryParse(raw, ignoreCase: true, out LazyLibrarianOrigin parsed)
            ? parsed
            : LazyLibrarianOrigin.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, LazyLibrarianOrigin value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
