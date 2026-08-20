using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.LazyLibrarian;

/// <summary>
/// The envelope LazyLibrarian returns when a command fails.
/// It answers HTTP 200, so the body is the only signal.
/// </summary>
public sealed record LazyLibrarianApiResponse
{
    [JsonPropertyName("Success")]
    public bool Success { get; init; }

    [JsonPropertyName("Error")]
    public LazyLibrarianApiError? Error { get; init; }
}
