using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.LazyLibrarian;

public sealed record LazyLibrarianDownloadProgressResponse
{
    [JsonPropertyName("Data")]
    public LazyLibrarianDownloadProgress? Data { get; init; }
}
