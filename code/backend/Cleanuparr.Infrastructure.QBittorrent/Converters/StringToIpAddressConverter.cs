using System;
using System.Net;
using Newtonsoft.Json;

namespace QBittorrent.Client.Converters
{
    internal class StringToIpAddressConverter : JsonConverter<IPAddress>
    {
        public override void WriteJson(JsonWriter writer, IPAddress value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(value.ToString());
        }

        public override IPAddress ReadJson(JsonReader reader, Type objectType, IPAddress existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                return IPAddress.Parse(reader.Value.ToString());
            }

            throw new JsonSerializationException($"Unexpected token {reader.TokenType}.");
        }
    }
}
