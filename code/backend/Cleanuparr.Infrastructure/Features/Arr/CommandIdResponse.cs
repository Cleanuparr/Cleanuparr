using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Arr;

internal sealed class CommandIdResponse
{
    [JsonPropertyName("id")]
    public long? Id { get; init; }
}
