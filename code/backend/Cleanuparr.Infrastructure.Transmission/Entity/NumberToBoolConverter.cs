using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Transmission.API.RPC.Entity
{
    /// <summary>
    /// Reads a JSON boolean or a 0/1 number as a bool.
    /// </summary>
    public class NumberToBoolConverter : JsonConverter<bool>
    {
        /// <inheritdoc/>
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetInt64() != 0;
            }

            return reader.GetBoolean();
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }
}
