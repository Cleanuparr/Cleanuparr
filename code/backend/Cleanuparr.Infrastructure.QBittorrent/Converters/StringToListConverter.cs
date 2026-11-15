using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace QBittorrent.Client.Converters
{
    internal class StringToListConverter : JsonConverter<IReadOnlyList<string>>
    {
        private readonly string _separator;
        private readonly bool _trim;

        public StringToListConverter(string separator = "\n", bool trim = false)
        {
            _separator = separator;
            _trim = trim;
        }

        public override void WriteJson(JsonWriter writer, IReadOnlyList<string> value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(string.Join(_separator, value));
        }

        public override IReadOnlyList<string> ReadJson(JsonReader reader, Type objectType, IReadOnlyList<string> existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            if (reader.TokenType == JsonToken.String)
            {
                var list = reader.Value.ToString().Split(new[] {_separator}, StringSplitOptions.RemoveEmptyEntries);
                if (_trim)
                {
                    list = list.Select(x => x.Trim()).ToArray();
                }

                return list;
            }

            throw new JsonSerializationException($"Unexpected token {reader.TokenType}.");
        }
    }
}
