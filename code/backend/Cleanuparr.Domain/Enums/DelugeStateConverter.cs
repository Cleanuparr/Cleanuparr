using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Maps Deluge wire state strings to <see cref="DelugeState"/>, falling back to <see cref="DelugeState.Unknown"/> for any value not present in the enum
/// </summary>
public sealed class DelugeStateConverter : JsonConverter<DelugeState>
{
    public override bool HandleNull => true;

    public override DelugeState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return DelugeState.Unknown;
        }

        string? raw = reader.GetString();

        return raw is not null && Enum.TryParse(raw, ignoreCase: true, out DelugeState parsed)
            ? parsed
            : DelugeState.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, DelugeState value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
