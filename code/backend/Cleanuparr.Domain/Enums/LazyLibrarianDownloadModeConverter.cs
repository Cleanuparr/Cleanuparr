using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

public sealed class LazyLibrarianDownloadModeConverter : JsonConverter<LazyLibrarianDownloadMode>
{
    public override bool HandleNull => true;

    public override LazyLibrarianDownloadMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return LazyLibrarianDownloadMode.Unknown;
        }

        string? raw = reader.GetString();

        return raw is not null
               && raw.All(char.IsAsciiLetter)
               && Enum.TryParse(raw, ignoreCase: true, out LazyLibrarianDownloadMode parsed)
            ? parsed
            : LazyLibrarianDownloadMode.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, LazyLibrarianDownloadMode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
