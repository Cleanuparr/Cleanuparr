using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Cleanuparr.Infrastructure.Features.Arr;

/// <summary>
/// Streaming JSON deserialization helper backed by <see cref="System.Text.Json"/> with fewer allocations and faster for large array responses.
/// </summary>
internal static class JsonStreamReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Streams items out of a top-level JSON array.
    /// </summary>
    public static async IAsyncEnumerable<T> StreamArrayAsync<T>(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (T? item in JsonSerializer.DeserializeAsyncEnumerable<T>(stream, Options, cancellationToken))
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }
}
