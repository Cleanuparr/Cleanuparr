using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace QBittorrent.Client.Converters
{
    internal class SeparatedStringToListConverter : JsonConverter<IEnumerable<string>>
    {
        private readonly string[] _separators;

        public SeparatedStringToListConverter(string separator) 
            : this(new [] { separator} )
        {
        }

        public SeparatedStringToListConverter(string separator1, string separator2)
            : this(new[] { separator1, separator2 })
        {
        }

        public SeparatedStringToListConverter(string separator1, string separator2, string separator3)
            : this(new[] { separator1, separator2, separator3 })
        {
        }

        public SeparatedStringToListConverter(params string[] separators)
        {
            if (separators == null)
                throw new ArgumentNullException(nameof(separators));
            if (!separators.Any())
                throw new ArgumentException("At least one separator must be provided", nameof(separators));
            if (separators.Any(string.IsNullOrEmpty))
                throw new ArgumentException("A separator cannot be an empty string.", nameof(separators));

            _separators = separators.ToArray();
        }

        public override void WriteJson(JsonWriter writer, IEnumerable<string> value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(string.Join(_separators[0], value));
        }

        public override IEnumerable<string> ReadJson(JsonReader reader, Type objectType, IEnumerable<string> existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                return reader.Value.ToString().Split(_separators, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            throw new JsonSerializationException($"Unexpected token {reader.TokenType}.");
        }
    }
}
