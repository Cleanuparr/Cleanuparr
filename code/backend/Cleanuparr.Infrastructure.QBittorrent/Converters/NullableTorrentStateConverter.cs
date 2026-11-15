using System;
using Newtonsoft.Json;

namespace QBittorrent.Client.Converters;

internal class NullableTorrentStateConverter : JsonConverter<TorrentState?>
{
    private readonly TorrentStateConverter _inner = new();

    public override void WriteJson(JsonWriter writer, TorrentState? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        _inner.WriteJson(writer, value.Value, serializer);
    }

    public override TorrentState? ReadJson(JsonReader reader, Type objectType, TorrentState? existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        return _inner.ReadJson(reader, typeof(TorrentState), default, false, serializer);
    }
}
