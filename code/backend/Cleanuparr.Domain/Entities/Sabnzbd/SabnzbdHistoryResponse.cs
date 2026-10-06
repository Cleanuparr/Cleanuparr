using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Sabnzbd;

public sealed record SabnzbdHistoryResponse
{
    [JsonPropertyName("history")]
    public SabnzbdHistoryData? History { get; init; }
}
