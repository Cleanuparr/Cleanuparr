using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Maps the LazyLibrarian Source column to <see cref="LazyLibrarianSource"/> and back.
/// The map is explicit because two wire values carry an underscore.
/// </summary>
public sealed class LazyLibrarianSourceConverter : JsonConverter<LazyLibrarianSource>
{
    private static readonly Dictionary<string, LazyLibrarianSource> ByWireValue = new(StringComparer.OrdinalIgnoreCase)
    {
        ["QBITTORRENT"] = LazyLibrarianSource.QBittorrent,
        ["TRANSMISSION"] = LazyLibrarianSource.Transmission,
        ["DELUGEWEBUI"] = LazyLibrarianSource.DelugeWebUi,
        ["DELUGERPC"] = LazyLibrarianSource.DelugeRpc,
        ["UTORRENT"] = LazyLibrarianSource.UTorrent,
        ["RTORRENT"] = LazyLibrarianSource.RTorrent,
        ["BLACKHOLE"] = LazyLibrarianSource.Blackhole,
        ["DIRECT"] = LazyLibrarianSource.Direct,
        ["IRC"] = LazyLibrarianSource.Irc,
        ["SYNOLOGY"] = LazyLibrarianSource.Synology,
        ["SYNOLOGY_TOR"] = LazyLibrarianSource.SynologyTorrent,
        ["SYNOLOGY_NZB"] = LazyLibrarianSource.SynologyNzb,
        ["SABNZBD"] = LazyLibrarianSource.Sabnzbd,
        ["NZBGET"] = LazyLibrarianSource.NzbGet,
    };

    private static readonly Dictionary<LazyLibrarianSource, string> ByMember =
        ByWireValue.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static string ToWireValue(LazyLibrarianSource source) =>
        ByMember.TryGetValue(source, out string? wire) ? wire : string.Empty;

    public override bool HandleNull => true;

    public override LazyLibrarianSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return LazyLibrarianSource.Unknown;
        }

        string? raw = reader.GetString();

        return raw is not null && ByWireValue.TryGetValue(raw, out LazyLibrarianSource parsed)
            ? parsed
            : LazyLibrarianSource.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, LazyLibrarianSource value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ToWireValue(value));
    }
}
