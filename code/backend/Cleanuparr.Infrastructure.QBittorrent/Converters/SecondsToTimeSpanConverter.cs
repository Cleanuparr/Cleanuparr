using System;
using System.Numerics;
using Newtonsoft.Json;

namespace QBittorrent.Client.Converters
{
    internal class SecondsToTimeSpanConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteValue(-1);
                return;
            }

            writer.WriteValue((long)((TimeSpan)value).TotalSeconds);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            if (reader.TokenType == JsonToken.Integer)
            {
                if (reader.Value is BigInteger)
                    return TimeSpan.MaxValue;

                long totalSeconds = Convert.ToInt64(reader.Value);
                if (totalSeconds >= 0)
                {
                    return totalSeconds > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond
                        ? TimeSpan.MaxValue
                        : new TimeSpan(totalSeconds * TimeSpan.TicksPerSecond);
                }
                else
                {
                    return totalSeconds < TimeSpan.MinValue.Ticks / TimeSpan.TicksPerSecond
                        ? TimeSpan.MinValue
                        : new TimeSpan(totalSeconds * TimeSpan.TicksPerSecond);
                }
            }

            throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing integer.");
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TimeSpan?);
        }
    }
}
