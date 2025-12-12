using Newtonsoft.Json;

namespace Cleanuparr.Infrastructure.Features.Notifications.Pushover;

public sealed record PushoverResponse
{
    [JsonProperty("status")]
    public int Status { get; init; }

    [JsonProperty("request")]
    public string? Request { get; init; }

    [JsonProperty("receipt")]
    public string? Receipt { get; init; }

    [JsonProperty("errors")]
    public List<string>? Errors { get; init; }

    public bool IsSuccess => Status == 1;
}
