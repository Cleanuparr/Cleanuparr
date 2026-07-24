using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Pushover;

public sealed record PushoverResponse
{
    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("request")]
    public string? Request { get; init; }

    [JsonPropertyName("receipt")]
    public string? Receipt { get; init; }

    [JsonPropertyName("errors")]
    public List<string>? Errors { get; init; }

    public bool IsSuccess => Status == 1;
}
