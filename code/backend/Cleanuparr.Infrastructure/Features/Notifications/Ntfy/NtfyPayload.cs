using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Ntfy;

public sealed class NtfyPayload
{
    [JsonPropertyName("topic")]
    public string Topic { get; init; } = string.Empty;
    
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
    
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    
    [JsonPropertyName("priority")]
    public int? Priority { get; init; }
    
    [JsonPropertyName("tags")]
    public string[]? Tags { get; init; }
    
    [JsonPropertyName("click")]
    public string? Click { get; init; }
}
