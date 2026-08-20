using System.Text.Json;
using Cleanuparr.Domain.Entities.LazyLibrarian;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Json;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.LazyLibrarian;

public sealed class LazyLibrarianService : ILazyLibrarianService
{
    private const string AudioBookLibrary = "AudioBook";

    private readonly ILogger<LazyLibrarianService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IDryRunInterceptor _dryRunInterceptor;

    public LazyLibrarianService(
        ILogger<LazyLibrarianService> logger,
        IHttpClientFactory httpClientFactory,
        IDryRunInterceptor dryRunInterceptor
    )
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
        _dryRunInterceptor = dryRunInterceptor;
    }

    public async Task<IReadOnlyList<LazyLibrarianQueueItem>> GetQueueAsync(ArrInstance instance)
    {
        List<LazyLibrarianWantedRecord> rows = await GetHistoryAsync(instance);
        HashSet<string> adoptedHashes = FindAdoptedHashes(rows);

        List<LazyLibrarianQueueItem> items = new();

        foreach (LazyLibrarianWantedRecord row in rows)
        {
            if (!IsActionableBook(row))
            {
                continue;
            }

            items.Add(new LazyLibrarianQueueItem
            {
                DownloadId = row.DownloadId!,
                Title = row.Title ?? string.Empty,
                BookId = row.BookId!,
                Library = row.Library,
                Source = row.Source,
                Origin = adoptedHashes.Contains(row.DownloadId!)
                    ? LazyLibrarianOrigin.Adopted
                    : LazyLibrarianOrigin.New,
            });
        }

        return items;
    }

    public async Task<IReadOnlyList<string>> GetClaimedHashesAsync(ArrInstance instance)
    {
        List<LazyLibrarianWantedRecord> rows = await GetHistoryAsync(instance);

        return rows
            .Where(IsSnatchedTorrent)
            .Select(row => row.DownloadId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<LazyLibrarianDownloadProgress?> GetDownloadProgressAsync(ArrInstance instance, LazyLibrarianQueueItem item)
    {
        Uri uri = BuildApiUri(
            instance,
            "getDownloadProgress",
            ("source", item.Source.ToWireValue()),
            ("downloadid", item.DownloadId)
        );

        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        using HttpResponseMessage response = await _httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync();

        EnsureNoApiError(body, instance, "download progress");

        LazyLibrarianDownloadProgressResponse? progress =
            JsonSerializer.Deserialize<LazyLibrarianDownloadProgressResponse>(body, CleanuparrJsonOptions.ExternalApiRead);

        return progress?.Data;
    }

    public async Task ResetItemAsync(ArrInstance instance, LazyLibrarianQueueItem item)
    {
        Uri uri = item.IsAudioBook
            ? BuildApiUri(instance, "queueBook", ("id", item.BookId), ("type", AudioBookLibrary))
            : BuildApiUri(instance, "queueBook", ("id", item.BookId));

        await SendCommandAsync(instance, uri, "queue item reset", item.Title);
    }

    public async Task TriggerSearchAsync(ArrInstance instance, LazyLibrarianQueueItem item)
    {
        Uri uri = BuildApiUri(instance, "searchBook", ("id", item.BookId));

        await SendCommandAsync(instance, uri, "book search", item.Title);
    }

    public async Task HealthCheckAsync(ArrInstance instance)
    {
        Uri uri = BuildApiUri(instance, "getVersion");

        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        using HttpResponseMessage response = await _httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync();

        EnsureNoApiError(body, instance, "connection test");

        LazyLibrarianApiResponse? version =
            JsonSerializer.Deserialize<LazyLibrarianApiResponse>(body, CleanuparrJsonOptions.ExternalApiRead);

        if (version?.Success is not true)
        {
            throw new Exception($"unrecognized version response | {instance.Url}");
        }

        _logger.LogDebug("Connection test successful for {url}", instance.Url);
    }

    private async Task<List<LazyLibrarianWantedRecord>> GetHistoryAsync(ArrInstance instance)
    {
        Uri uri = BuildApiUri(instance, "getHistory");

        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            _logger.LogError("queue list failed | {url}", instance.Url);
            throw;
        }

        string body = await response.Content.ReadAsStringAsync();

        EnsureNoApiError(body, instance, "queue list");

        if (!body.TrimStart().StartsWith('['))
        {
            throw new Exception($"unrecognized queue list response | {instance.Url}");
        }

        List<LazyLibrarianWantedRecord>? rows =
            JsonSerializer.Deserialize<List<LazyLibrarianWantedRecord>>(body, CleanuparrJsonOptions.ExternalApiRead);

        if (rows is null)
        {
            throw new Exception($"unrecognized queue list response | {instance.Url}");
        }

        return rows;
    }

    private async Task SendCommandAsync(ArrInstance instance, Uri uri, string context, string title)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, uri);

            HttpResponseMessage? response = await _dryRunInterceptor.InterceptAsync(() => SendAsync(request));

            if (response is null)
            {
                return;
            }

            try
            {
                await EnsureCommandAcceptedAsync(response, instance, context);
            }
            finally
            {
                response.Dispose();
            }
        }
        catch
        {
            _logger.LogError("{context} failed | {url} | {title}", context, instance.Url, title);
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        HttpResponseMessage response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return response;
    }

    /// <summary>
    /// LazyLibrarian judges the group sharing a DownloadId, not the row.
    /// </summary>
    private static HashSet<string> FindAdoptedHashes(List<LazyLibrarianWantedRecord> rows)
    {
        return rows
            .Where(row => !string.IsNullOrEmpty(row.DownloadId))
            .Where(row => row.Origin is not LazyLibrarianOrigin.New)
            .Select(row => row.DownloadId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSnatchedTorrent(LazyLibrarianWantedRecord row)
    {
        if (row.Status is not LazyLibrarianStatus.Snatched)
        {
            return false;
        }

        if (string.IsNullOrEmpty(row.DownloadId))
        {
            return false;
        }

        if (row.Mode is not (LazyLibrarianDownloadMode.Torrent or LazyLibrarianDownloadMode.Torznab or LazyLibrarianDownloadMode.Magnet))
        {
            return false;
        }

        return row.Source.IsTorrentClient();
    }

    private static bool IsActionableBook(LazyLibrarianWantedRecord row)
    {
        if (!IsSnatchedTorrent(row))
        {
            return false;
        }

        if (string.IsNullOrEmpty(row.BookId))
        {
            return false;
        }

        // A magazine BookID is a title and a comic BookID carries an issue key.
        // The book commands reject both.
        if (row.Library is not (BookLibrary.EBook or BookLibrary.AudioBook))
        {
            return false;
        }

        // A legacy row that was never matched to a book.
        return !string.Equals(row.BookId, "unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri BuildApiUri(ArrInstance instance, string command, params (string Key, string Value)[] extraParameters)
    {
        UriBuilder uriBuilder = new(instance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api";

        List<string> parts =
        [
            $"apikey={Uri.EscapeDataString(instance.ApiKey)}",
            $"cmd={Uri.EscapeDataString(command)}",
        ];

        foreach ((string key, string value) in extraParameters)
        {
            parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }

        uriBuilder.Query = string.Join("&", parts);

        return uriBuilder.Uri;
    }

    /// <summary>
    /// LazyLibrarian answers HTTP 200 for a rejected command, so the body is the only signal.
    /// </summary>
    private static void EnsureNoApiError(string body, ArrInstance instance, string context)
    {
        string trimmed = body.TrimStart();

        // A command answers with a bare string and the queue with an array.
        // Only an object can carry the error envelope.
        if (!trimmed.StartsWith('{'))
        {
            return;
        }

        LazyLibrarianApiResponse? error =
            JsonSerializer.Deserialize<LazyLibrarianApiResponse>(trimmed, CleanuparrJsonOptions.ExternalApiRead);

        if (error?.Success is false)
        {
            throw new Exception($"{context} failed | {instance.Url} | {error.Error?.Message ?? "unknown error"}");
        }
    }

    /// <summary>
    /// An unknown book id answers "Invalid id" and a read-only key is refused, both with HTTP 200.
    /// </summary>
    private async Task EnsureCommandAcceptedAsync(HttpResponseMessage response, ArrInstance instance, string context)
    {
        string body = (await response.Content.ReadAsStringAsync()).Trim();

        EnsureNoApiError(body, instance, context);

        if (string.Equals(body, "OK", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // The body can echo the request URI, which carries the api key.
        _logger.LogError("{context} was refused | {url} | {body}", context, instance.Url, body);

        throw new Exception($"{context} failed | {instance.Url}");
    }
}
