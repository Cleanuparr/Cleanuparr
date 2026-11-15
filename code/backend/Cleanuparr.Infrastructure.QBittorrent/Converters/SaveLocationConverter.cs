using System;
using Newtonsoft.Json;

namespace QBittorrent.Client.Converters
{
    internal class SaveLocationConverter : JsonConverter<SaveLocation>
    {
        public override void WriteJson(JsonWriter writer, SaveLocation value, JsonSerializer serializer)
        {
            if (value.StandardFolder != null)
            {
                writer.WriteValue(value.StandardFolder);
            }
            else if (!string.IsNullOrWhiteSpace(value.CustomFolder))
            {
                writer.WriteValue(value.CustomFolder);
            }
            else
            {
                throw new JsonSerializationException("Invalid save location object.");
            }
        }

        public override SaveLocation ReadJson(JsonReader reader, Type objectType, SaveLocation existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Integer)
            {
                return new SaveLocation((StandardSaveLocation)Convert.ToInt32(reader.Value));
            }
            if (reader.TokenType == JsonToken.String)
            {
                return new SaveLocation(reader.Value.ToString());
            }

            throw new JsonSerializationException($"Unexpected token {reader.TokenType}.");
        }
    }
}
