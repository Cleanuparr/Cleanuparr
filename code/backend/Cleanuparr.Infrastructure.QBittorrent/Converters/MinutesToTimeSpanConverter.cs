using System;
using System.Numerics;
using Newtonsoft.Json;

namespace QBittorrent.Client.Converters
{
    internal class MinutesToTimeSpanConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteValue(-1);
                return;
            }

            writer.WriteValue((long)((TimeSpan)value).TotalMinutes);
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

                long totalMinutes = Convert.ToInt64(reader.Value);
                if (totalMinutes >= 0)
                {
                    return totalMinutes > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMinute
                        ? TimeSpan.MaxValue
                        : new TimeSpan(totalMinutes * TimeSpan.TicksPerMinute);
                }
                else
                {
                    return totalMinutes < TimeSpan.MinValue.Ticks / TimeSpan.TicksPerMinute
                        ? TimeSpan.MinValue
                        : new TimeSpan(totalMinutes * TimeSpan.TicksPerMinute);
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
