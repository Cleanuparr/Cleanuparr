using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.LazyLibrarian;

/// <summary>
/// The error LazyLibrarian reports inside a successful HTTP response.
/// </summary>
public sealed record LazyLibrarianApiError
{
    [JsonPropertyName("Code")]
    public int Code { get; init; }

    [JsonPropertyName("Message")]
    public string? Message { get; init; }
}
