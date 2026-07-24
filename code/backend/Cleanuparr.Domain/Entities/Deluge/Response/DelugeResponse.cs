using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Deluge.Response;

public sealed record DelugeResponse<T>
{
    [JsonPropertyName("id")]
    public int ResponseId { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }

    [JsonPropertyName("error")]
    public DelugeError? Error { get; set; }
}
