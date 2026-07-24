using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Deluge.Request;

public class DelugeRequest
{
    [JsonPropertyName("id")]
    public int RequestId { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("params")]
    public List<object> Params { get; set; }

    public DelugeRequest(int requestId, string method, params object[]? parameters)
    {
        RequestId = requestId;
        Method = method;
        Params = [];

        if (parameters != null)
        {
            Params.AddRange(parameters);
        }
    }
}
