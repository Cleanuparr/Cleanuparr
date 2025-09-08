using Newtonsoft.Json;

namespace Cleanuparr.Infrastructure.Features.Notifications.Ntfy;

public sealed class NtfyPayload
{
    [JsonProperty("topic")]
    public string Topic { get; init; } = string.Empty;
    
    [JsonProperty("message")]
    public string Message { get; init; } = string.Empty;
    
    [JsonProperty("title")]
    public string? Title { get; init; }
    
    [JsonProperty("priority")]
    public int? Priority { get; init; }
    
    [JsonProperty("tags")]
    public string[]? Tags { get; init; }
    
    [JsonProperty("click")]
    public string? Click { get; init; }
}
