using System.Runtime.CompilerServices;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.History;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Json;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Arr;

public class ChaptarrClient : ReadarrClient, IChaptarrClient
{
    public ChaptarrClient(
        ILogger<ChaptarrClient> logger,
        IHttpClientFactory httpClientFactory,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor
    ) : base(logger, httpClientFactory, striker, dryRunInterceptor)
    {
    }

    public override async Task<List<Tag>> GetAllTagsAsync(ArrInstance arrInstance)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/{ApiVersion}/tag";
        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        return await DeserializeStreamAsync<List<Tag>>(response) ?? [];
    }

    public async IAsyncEnumerable<SearchableAuthor> StreamAllAuthorsAsync(
        ArrInstance arrInstance,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/{ApiVersion}/author";

        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await foreach (SearchableAuthor author in JsonStreamReader.StreamArrayAsync<SearchableAuthor>(stream, cancellationToken))
        {
            yield return author;
        }
    }

    /// <inheritdoc/>
    public async Task<HashSet<long>> GetRecentlyGrabbedBookIdsAsync(ArrInstance arrInstance, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        List<ArrHistoryRecord> records = await GetGrabHistorySinceAsync(arrInstance, since, string.Empty, cancellationToken);

        return records
            .Where(record => record.BookId > 0)
            .Select(record => record.BookId)
            .ToHashSet();
    }

    /// <inheritdoc/>
    public async Task<List<SearchableBook>> GetMissingBooksAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default) =>
        await GetAllWantedAsync<SearchableBook>(arrInstance, "missing", monitored, cancellationToken);

    /// <inheritdoc/>
    public async Task<List<SearchableBook>> GetCutoffUnmetBooksAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default) =>
        await GetAllWantedAsync<SearchableBook>(arrInstance, "cutoff", monitored, cancellationToken);
}
