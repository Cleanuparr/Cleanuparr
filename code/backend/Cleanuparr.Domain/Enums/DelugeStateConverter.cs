using Newtonsoft.Json;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Maps Deluge wire state strings to <see cref="DelugeState"/>, falling back to <see cref="DelugeState.Unknown"/> for any value not present in the enum
/// </summary>
public sealed class DelugeStateConverter : JsonConverter<DelugeState>
{
    public override DelugeState ReadJson(JsonReader reader, Type objectType, DelugeState existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String || reader.Value is not string raw)
        {
            return DelugeState.Unknown;
        }

        return Enum.TryParse<DelugeState>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : DelugeState.Unknown;
    }

    public override void WriteJson(JsonWriter writer, DelugeState value, JsonSerializer serializer)
    {
        writer.WriteValue(value.ToString());
    }
}
