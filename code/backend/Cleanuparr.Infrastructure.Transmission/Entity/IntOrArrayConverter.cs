using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Transmission.API.RPC.Entity
{
    /// <summary>
    /// Reads a JSON number or array as a long array.
    /// </summary>
    public class IntOrArrayConverter : JsonConverter<long[]>
    {
        /// <inheritdoc/>
        public override long[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return new long[] { reader.GetInt64() };
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                return JsonSerializer.Deserialize<long[]>(ref reader, options);
            }

            throw new JsonException("Unexpected token type");
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, long[]? value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
