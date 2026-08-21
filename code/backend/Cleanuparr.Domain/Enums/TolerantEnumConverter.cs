using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Maps a wire string to <typeparamref name="TEnum"/> and back.
/// An unknown value reads as the zero member, which every such enum names Unknown.
/// </summary>
public sealed class TolerantEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override bool HandleNull => true;

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            return default;
        }

        string? raw = reader.GetString();

        return raw is not null
               && raw.All(char.IsAsciiLetter)
               && Enum.TryParse(raw, ignoreCase: true, out TEnum parsed)
            ? parsed
            : default;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
