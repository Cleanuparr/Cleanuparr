using System;
using Newtonsoft.Json;

namespace QBittorrent.Client.Converters
{
    internal class UnixTimeToNullableDateTimeConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var dateTime = new DateTime(((DateTime)value).Ticks, DateTimeKind.Utc);
                var dateTimeOffset = new DateTimeOffset(dateTime, TimeSpan.Zero);
                writer.WriteValue(dateTimeOffset.ToUnixTimeSeconds());
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }
            if (reader.TokenType == JsonToken.Integer)
            {
                var unixTime = Convert.ToInt64(reader.Value);
                if (unixTime <= 0)
                    return null;

                // Some torrents have malformed timestamps that fall outside DateTime's representable range
                const long MinUnixSeconds = -62135596800L;
                const long MaxUnixSeconds = 253402300799L;
                if (unixTime > MaxUnixSeconds || unixTime < MinUnixSeconds)
                {
                    return null;
                }

                var dto = DateTimeOffset.FromUnixTimeSeconds(unixTime);
                return new DateTime(dto.UtcTicks, DateTimeKind.Unspecified);
            }

            throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing unix time.");
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(DateTime?);
    }
}
