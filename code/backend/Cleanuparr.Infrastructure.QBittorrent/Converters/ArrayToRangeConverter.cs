using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace QBittorrent.Client.Converters
{
    internal class ArrayToRangeConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var range = (Range) value;
            writer.WriteStartArray();
            writer.WriteValue(range.StartIndex);
            writer.WriteValue(range.EndIndex);
            writer.WriteEndArray();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                if (objectType != typeof(Range?))
                    throw new JsonSerializationException("Not-null value was expected.");

                return null;
            }

            if (reader.TokenType == JsonToken.StartArray)
            {
                var list = new List<long>(2);
                while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                {
                    if (reader.TokenType != JsonToken.Integer)
                        throw new JsonSerializationException($"Unexpected token {reader.TokenType}.");
                    list.Add(Convert.ToInt64(reader.Value));
                }

                if (list.Count != 2)
                    throw new JsonSerializationException("Expected array of 2 elements.");
                return new Range(list[0], list[1]);
            }

            throw new JsonSerializationException($"Unexpected token {reader.TokenType}.");
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Range) || objectType == typeof(Range?);
        }
    }
}
