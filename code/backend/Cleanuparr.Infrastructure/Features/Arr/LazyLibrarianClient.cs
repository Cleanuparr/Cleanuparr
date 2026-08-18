using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Entities.LazyLibrarian;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Arr;

public sealed class LazyLibrarianClient : ArrClient, ILazyLibrarianClient
{
    private static readonly string[] TorrentModes = ["torrent", "torznab", "magnet"];

    public LazyLibrarianClient(
        ILogger<LazyLibrarianClient> logger,
        IHttpClientFactory httpClientFactory,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor
    ) : base(logger, httpClientFactory, striker, dryRunInterceptor)
    {
    }

    public override async Task<QueueListResponse> GetQueueItemsAsync(ArrInstance arrInstance, int page)
    {
        if (page > 1)
        {
            return new QueueListResponse
            {
                TotalRecords = 0,
                Records = Array.Empty<QueueRecord>(),
            };
        }

        Uri uri = BuildApiUri(arrInstance, "getHistory");

        using HttpRequestMessage request = new(HttpMethod.Get, uri);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            _logger.LogError("queue list failed | {url}", arrInstance.Url);
            throw;
        }

        List<LazyLibrarianWantedRecord>? rows = await DeserializeStreamAsync<List<LazyLibrarianWantedRecord>>(response);

        if (rows is null)
        {
            throw new Exception($"unrecognized queue list response | {arrInstance.Url}");
        }

        List<QueueRecord> records = new();

        foreach (LazyLibrarianWantedRecord row in rows)
        {
            if (!IsSnatchedTorrent(row))
            {
                continue;
            }

            if (string.IsNullOrEmpty(row.DownloadId) || string.IsNullOrEmpty(row.BookId))
            {
                continue;
            }

            if (string.Equals(row.BookId, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                // Magazines are stored without a book id.
                continue;
            }

            records.Add(new QueueRecord
            {
                ContentId = row.BookId,
                Title = row.Title ?? string.Empty,
                DownloadId = row.DownloadId,
                Protocol = "torrent",
                Status = "snatched",
                TrackedDownloadStatus = string.Empty,
                TrackedDownloadState = string.Empty,
            });
        }

        return new QueueListResponse
        {
            TotalRecords = records.Count,
            Records = records,
        };
    }

    public override Task<bool> ShouldRemoveFromQueue(InstanceType instanceType, QueueRecord record, bool isPrivateDownload, short arrMaxStrikes)
    {
        // LazyLibrarian handles failed imports internally
        return Task.FromResult(false);
    }

    public override async Task DeleteQueueItemAsync(
        ArrInstance arrInstance,
        QueueRecord record,
        bool removeFromClient,
        bool changeCategory,
        DeleteReason deleteReason
    )
    {
        if (string.IsNullOrWhiteSpace(record.ContentId))
        {
            _logger.LogWarning("queue item reset skipped | no book id | {url} | {title}", arrInstance.Url, record.Title);
            return;
        }

        Uri uri = BuildApiUri(arrInstance, "queueBook", ("id", record.ContentId));

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, uri);

            HttpResponseMessage? response = await _dryRunInterceptor.InterceptAsync(() => SendRequestAsync(request));
            response?.Dispose();

            _logger.LogInformation(
                "queue item reset in LazyLibrarian with reason {reason} | {url} | {title}",
                deleteReason.ToString(),
                arrInstance.Url,
                record.Title
            );
        }
        catch
        {
            _logger.LogError("queue delete failed | {url} | {title}", arrInstance.Url, record.Title);
            throw;
        }
    }

    public override async Task<List<long>> SearchItemsAsync(ArrInstance arrInstance, HashSet<SearchItem>? items)
    {
        if (items?.Count is null or 0)
        {
            return [];
        }

        foreach (SearchItem item in items)
        {
            if (item is not BookSearchItem book || string.IsNullOrWhiteSpace(book.ContentId))
            {
                _logger.LogWarning("book search skipped | no book id | {url}", arrInstance.Url);
                continue;
            }

            Uri uri = BuildApiUri(arrInstance, "searchBook", ("id", book.ContentId));

            using HttpRequestMessage request = new(HttpMethod.Get, uri);

            try
            {
                HttpResponseMessage? response = await _dryRunInterceptor.InterceptAsync(() => SendRequestAsync(request));
                response?.Dispose();

                _logger.LogInformation("book search triggered | {url} | book id: {id}", arrInstance.Url, book.ContentId);
            }
            catch
            {
                _logger.LogError("book search failed | {url} | book id: {id}", arrInstance.Url, book.ContentId);
                throw;
            }
        }

        return [];
    }

    public override async Task<long> SearchItemAsync(ArrInstance arrInstance, SearchItem item)
    {
        await SearchItemsAsync(arrInstance, [item]);

        // LazyLibrarian returns no command id to track
        return 0;
    }

    public override bool HasContentId(QueueRecord record) => !string.IsNullOrWhiteSpace(record.ContentId);

    public override Task<List<Tag>> GetAllTagsAsync(ArrInstance arrInstance)
    {
        return Task.FromResult(new List<Tag>());
    }

    public override async Task HealthCheckAsync(ArrInstance arrInstance)
    {
        Uri uri = BuildApiUri(arrInstance, "getVersion");

        using HttpRequestMessage request = new(HttpMethod.Get, uri);

        using HttpResponseMessage response = await _httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        _logger.LogDebug("Connection test successful for {url}", arrInstance.Url);
    }

    public override Task<ArrCommandStatus> GetCommandStatusAsync(ArrInstance arrInstance, long commandId)
    {
        // LazyLibrarian has no async command model
        return Task.FromResult(new ArrCommandStatus(commandId, ArrCommandState.Completed, null));
    }

    protected override string GetSystemStatusUrlPath() => string.Empty;

    protected override string GetQueueUrlPath() => string.Empty;

    protected override string GetQueueUrlQuery(int page) => string.Empty;

    protected override string GetQueueDeleteUrlPath(long recordId) => string.Empty;

    protected override void SetApiKey(HttpRequestMessage request, string apiKey)
    {
        // LazyLibrarian expects the API key as a query parameter
    }

    private static Uri BuildApiUri(ArrInstance arrInstance, string command, params (string Key, string? Value)[] extraParameters)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api";

        List<string> parts = new()
        {
            $"apikey={Uri.EscapeDataString(arrInstance.ApiKey)}",
            $"cmd={Uri.EscapeDataString(command)}",
        };

        foreach ((string key, string? value) in extraParameters)
        {
            parts.Add(value is null
                ? Uri.EscapeDataString(key)
                : $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }

        uriBuilder.Query = string.Join("&", parts);

        return uriBuilder.Uri;
    }

    private static bool IsSnatchedTorrent(LazyLibrarianWantedRecord record)
    {
        if (!string.Equals(record.Status, "Snatched", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // LazyLibrarian records the provider type, not the protocol.
        // It sends torznab, torrent and magnet to a torrent client.
        // An empty value stays rejected.
        // An nzb or direct row carries a job id, not a torrent hash.
        return TorrentModes.Contains(record.NzbMode, StringComparer.OrdinalIgnoreCase);
    }
}
