using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Maps a LazyLibrarian wire value to <see cref="LazyLibrarianStatus"/>.
/// Any value the enum does not name becomes <see cref="LazyLibrarianStatus.Unknown"/>.
/// </summary>
public sealed class LazyLibrarianStatusConverter : JsonConverter<LazyLibrarianStatus>
{
    public override bool HandleNull => true;

    public override LazyLibrarianStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return LazyLibrarianStatus.Unknown;
        }

        string? raw = reader.GetString();

        return raw is not null
               && raw.All(char.IsAsciiLetter)
               && Enum.TryParse(raw, ignoreCase: true, out LazyLibrarianStatus parsed)
            ? parsed
            : LazyLibrarianStatus.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, LazyLibrarianStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
