using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Maps arr command state strings to <see cref="ArrCommandState"/>, falling back to <see cref="ArrCommandState.Unknown"/> for any value not present in the enum
/// </summary>
public sealed class ArrCommandStateConverter : JsonConverter<ArrCommandState>
{
    public override bool HandleNull => true;

    public override ArrCommandState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return ArrCommandState.Unknown;
        }

        string? raw = reader.GetString();

        return raw is not null
               && raw.All(char.IsAsciiLetter)
               && Enum.TryParse(raw, ignoreCase: true, out ArrCommandState parsed)
            ? parsed
            : ArrCommandState.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, ArrCommandState value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
