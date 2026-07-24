using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Deluge.Response;

public record DelugeTorrent
{
    [JsonPropertyName("comment")]
    public string Comment { get; set; }

    [JsonPropertyName("is_seed")]
    public bool IsSeed { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("paused")]
    public bool Paused { get; set; }

    [JsonPropertyName("ratio")]
    public double Ratio { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; }
}