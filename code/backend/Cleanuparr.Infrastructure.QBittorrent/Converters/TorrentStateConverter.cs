using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace QBittorrent.Client.Converters;

internal class TorrentStateConverter : JsonConverter<TorrentState>
{
    private static readonly Dictionary<TorrentState, string> TorrentStateToString = BuildTorrentStateToString();
    private static readonly Dictionary<string, TorrentState> StringToTorrentState = BuildStringToTorrentState();

    private static Dictionary<TorrentState, string> BuildTorrentStateToString()
    {
        return (
            from field in typeof(TorrentState).GetTypeInfo().DeclaredFields
            where field.IsStatic
            let attribute = field.GetCustomAttribute<EnumMemberAttribute>()
            select (value: (TorrentState) field.GetValue(null), strValue: attribute.Value)
        ).ToDictionary(t => t.value, t => t.strValue);
    }

    private static Dictionary<string, TorrentState> BuildStringToTorrentState()
    {
        Dictionary<string, TorrentState> map = TorrentStateToString.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);
        map.Add("stoppedUP", TorrentState.PausedUpload);
        map.Add("stoppedDL", TorrentState.PausedDownload);
        return map;
    }

    public override void WriteJson(JsonWriter writer, TorrentState value, JsonSerializer serializer)
    {
        writer.WriteValue(TorrentStateToString.TryGetValue(value, out var s) ? s : value.ToString());
    }

    public override TorrentState ReadJson(JsonReader reader, Type objectType, TorrentState existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException($"Unexpected token {reader.TokenType}.");
            
        var stringValue = (reader.Value as string).ToUpperInvariant();
        if (StringToTorrentState.TryGetValue(stringValue, out var torrentState))
            return torrentState;

        if (Enum.TryParse(stringValue, out torrentState))
            return torrentState;

        throw new JsonSerializationException($"Unexpected torrent state \"{stringValue}\".");
    }
}