using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.LazyLibrarian;

public sealed record LazyLibrarianDownloadProgressResponse
{
    [JsonPropertyName("Success")]
    public bool Success { get; init; }

    [JsonPropertyName("Data")]
    public LazyLibrarianDownloadProgress? Data { get; init; }

    [JsonPropertyName("Error")]
    public LazyLibrarianApiError? Error { get; init; }
}
