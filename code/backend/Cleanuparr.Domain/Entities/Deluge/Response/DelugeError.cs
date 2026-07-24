using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Deluge.Response;

public sealed record DelugeError
{
    [JsonPropertyName("message")]
    public String Message { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; }
}
