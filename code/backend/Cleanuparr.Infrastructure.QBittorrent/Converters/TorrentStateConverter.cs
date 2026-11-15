using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace QBittorrent.Client.Converters;

internal class TorrentStateConverter : JsonConverter<TorrentState>
{
    private static readonly Dictionary<string, TorrentState> StringToTorrentState;
    private static readonly Dictionary<TorrentState, string> TorrentStateToString;

    static TorrentStateConverter()
    {
        TorrentStateToString = (
            from field in typeof(TorrentState).GetTypeInfo().DeclaredFields
            where field.IsStatic
            let attribute = field.GetCustomAttribute<EnumMemberAttribute>()
            select (value: (TorrentState) field.GetValue(null), strValue: attribute.Value)
        ).ToDictionary(t => t.value, t => t.strValue);

        StringToTorrentState = TorrentStateToString.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);
        StringToTorrentState.Add("stoppedUP", TorrentState.PausedUpload);
        StringToTorrentState.Add("stoppedDL", TorrentState.PausedDownload);
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
            
        var stringValue = (reader.Value as string)!.ToUpperInvariant();
        if (StringToTorrentState.TryGetValue(stringValue, out var torrentState))
            return torrentState;

        if (Enum.TryParse(stringValue, out torrentState))
            return torrentState;

        throw new JsonSerializationException($"Unexpected torrent state \"{stringValue}\".");

        //return stringValue switch
        //{
        //    "UNKNOWN" => TorrentState.Unknown,
        //    "ERROR" => TorrentState.Error,
        //    "PAUSEDUP" or "STOPPEDUP" => TorrentState.PausedUpload,
        //    "PAUSEDDL" or "STOPPEDDL" => TorrentState.PausedDownload,
        //    "QUEUEDUP" => TorrentState.QueuedUpload,
        //    "QUEUEDDL" => TorrentState.QueuedDownload,
        //    "UPLOADING" => TorrentState.Uploading,
        //    "STALLEDUP" => TorrentState.StalledUpload,
        //    "CHECKINGUP" => TorrentState.CheckingUpload,
        //    "CHECKINGDL" => TorrentState.CheckingDownload,
        //    "DOWNLOADING" => TorrentState.Downloading,
        //    "STALLEDDL" => TorrentState.StalledDownload,
        //    "METADL" => TorrentState.FetchingMetadata,
        //    "FORCEDMETADL" => TorrentState.ForcedFetchingMetadata,
        //    "FORCEDUP" => TorrentState.ForcedUpload,
        //    "FORCEDDL" => TorrentState.ForcedDownload,
        //    "MISSINGFILES" => TorrentState.MissingFiles,
        //    "ALLOCATING" => TorrentState.Allocating,
        //    "QUEUEDFORCHECKING" => TorrentState.QueuedForChecking,
        //    "CHECKINGRESUMEDATA" => TorrentState.CheckingResumeData,
        //    "MOVING" => TorrentState.Moving
        //};
    }
}