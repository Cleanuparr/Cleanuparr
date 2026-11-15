using System;
using System.Globalization;
using Newtonsoft.Json;

namespace QBittorrent.Client.Converters
{
    internal class Rfc2822ToDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
    {
        public override void WriteJson(JsonWriter writer, DateTimeOffset? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var dto = value.Value;
                // Note: RFC 2822 actually supports several formats, but it seems this one is enough for qBittorrent.
                var str = string.Format(CultureInfo.InvariantCulture,
                    "{0:ddd, dd MMM yyyy HH:mm:ss} {1}{2:hhmm}",
                    dto.DateTime,
                    dto.Offset >= TimeSpan.Zero ? '+' : '-',
                    dto.Offset);               
                writer.WriteValue(str);
            }
        }

        public override DateTimeOffset? ReadJson(JsonReader reader, Type objectType, DateTimeOffset? existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                var stringValue = reader.Value as string;
                if (string.IsNullOrWhiteSpace(stringValue))
                    return null;

                var culture = CultureInfo.InvariantCulture;
                var styles = DateTimeStyles.None;
                

                if (DateTimeOffset.TryParse(stringValue, culture, styles, out var dto))
                    return dto;

                return null;
            }

            throw new JsonSerializationException($"Unexpected token {reader.TokenType}.");
        }
    }
}