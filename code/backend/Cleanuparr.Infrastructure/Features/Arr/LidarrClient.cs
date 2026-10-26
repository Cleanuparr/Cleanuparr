using System.Runtime.CompilerServices;
using System.Text;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.History;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Entities.Lidarr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using System.Text.Json;
using Cleanuparr.Infrastructure.Json;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Arr;

public class LidarrClient : ArrClient, ILidarrClient
{
    public LidarrClient(
        ILogger<LidarrClient> logger,
        IHttpClientFactory httpClientFactory,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor
    ) : base(logger, httpClientFactory, striker, dryRunInterceptor)
    {
    }

    protected override string ApiVersion => "v1";

    protected override string GetSystemStatusUrlPath()
    {
        return $"/api/{ApiVersion}/system/status";
    }

    protected override string GetQueueUrlPath()
    {
        return $"/api/{ApiVersion}/queue";
    }

    protected override string GetQueueUrlQuery(int page)
    {
        return $"page={page}&pageSize={QueuePageSize}&includeUnknownArtistItems=true&includeArtist=true&includeAlbum=true";
    }

    protected override string GetQueueDeleteUrlPath(long recordId)
    {
        return $"/api/{ApiVersion}/queue/{recordId}";
    }

    public override async Task<List<long>> SearchItemsAsync(ArrInstance arrInstance, HashSet<SearchItem>? items)
    {
        if (items?.Count is null or 0)
        {
            return [];
        }

        List<long> commandIds = [];

        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}{CommandUrlPath}";

        foreach (var command in GetSearchCommands(items))
        {
            using HttpRequestMessage request = new(HttpMethod.Post, uriBuilder.Uri);
            request.Content = new StringContent(
                JsonSerializer.Serialize(command, CleanuparrJsonOptions.Outbound),
                Encoding.UTF8,
                "application/json"
            );
            SetApiKey(request, arrInstance.ApiKey);

            string? logContext = await ComputeCommandLogContextAsync(arrInstance, command);

            try
            {
                using HttpResponseMessage? response = await _dryRunInterceptor.InterceptAsync(() => SendRequestAsync(request));

                if (response is not null)
                {
                    long? commandId = await ReadCommandIdAsync(response);

                    if (commandId.HasValue)
                    {
                        commandIds.Add(commandId.Value);
                    }
                }

                _logger.LogInformation("{Log}", GetSearchLog(arrInstance.Url, command, true, logContext));
            }
            catch
            {
                _logger.LogError("{Log}", GetSearchLog(arrInstance.Url, command, false, logContext));
                throw;
            }
        }

        return commandIds;
    }

    public override bool HasContentId(QueueRecord record) => record.ArtistId is not 0 && record.AlbumId is not 0;

    private static string GetSearchLog(
        Uri instanceUrl,
        LidarrCommand command,
        bool success,
        string? logContext
    )
    {
        string status = success ? "triggered" : "failed";

        return $"album search {status} | {instanceUrl} | {logContext ?? $"albums: {string.Join(',', command.AlbumIds)}"}";
    }

    private async Task<string?> ComputeCommandLogContextAsync(ArrInstance arrInstance, LidarrCommand command)
    {
        try
        {
            StringBuilder log = new();

            var albums = await GetAlbumsAsync(arrInstance, command.AlbumIds);

            if (albums?.Count is null or 0) return null;

            var groups = albums
                .GroupBy(x => x.Artist.Id)
                .ToList();

            foreach (var group in groups)
            {
                var first = group.First();

                log.Append($"[{first.Artist.ArtistName} albums {string.Join(',', group.Select(x => x.Title).ToList())}]");
            }

            return log.ToString();
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "failed to compute log context");
        }

        return null;
    }

    private async Task<List<Album>?> GetAlbumsAsync(ArrInstance arrInstance, List<long> albumIds)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/{ApiVersion}/album";
        uriBuilder.Query = string.Join('&', albumIds.Select(x => $"albumIds={x}"));

        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        return await DeserializeStreamAsync<List<Album>>(response);
    }

    private List<LidarrCommand> GetSearchCommands(HashSet<SearchItem> items)
    {
        const string albumSearch = "AlbumSearch";

        return [new LidarrCommand { Name = albumSearch, AlbumIds = items.Select(i => i.Id).ToList() }];
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

    public async IAsyncEnumerable<SearchableArtist> StreamAllArtistsAsync(
        ArrInstance arrInstance,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/{ApiVersion}/artist";

        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await foreach (SearchableArtist artist in JsonStreamReader.StreamArrayAsync<SearchableArtist>(stream, cancellationToken))
        {
            yield return artist;
        }
    }

    public async Task<List<ArrQualityProfile>> GetQualityProfilesAsync(ArrInstance arrInstance)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/{ApiVersion}/qualityprofile";

        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        return await DeserializeStreamAsync<List<ArrQualityProfile>>(response) ?? [];
    }

    public async Task<List<SearchableAlbum>> GetAlbumsAsync(ArrInstance arrInstance, long artistId, CancellationToken cancellationToken = default)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/{ApiVersion}/album";
        uriBuilder.Query = $"artistId={artistId}";

        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await DeserializeStreamAsync<List<SearchableAlbum>>(response, cancellationToken) ?? [];
    }

    public async Task<List<ArrTrackFile>> GetTrackFilesAsync(ArrInstance arrInstance, long artistId, CancellationToken cancellationToken = default)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/{ApiVersion}/trackfile";
        uriBuilder.Query = $"artistId={artistId}";

        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await DeserializeStreamAsync<List<ArrTrackFile>>(response, cancellationToken) ?? [];
    }

    /// <inheritdoc/>
    public async Task<HashSet<long>> GetRecentlyGrabbedAlbumIdsAsync(ArrInstance arrInstance, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        List<ArrHistoryRecord> records = await GetGrabHistorySinceAsync(arrInstance, since, string.Empty, cancellationToken);

        return records
            .Where(record => record.AlbumId > 0)
            .Select(record => record.AlbumId)
            .ToHashSet();
    }

    /// <inheritdoc/>
    public async Task<List<SearchableAlbum>> GetMissingAlbumsAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default) =>
        await GetAllWantedAsync<SearchableAlbum>(arrInstance, "missing", monitored, cancellationToken);

    /// <inheritdoc/>
    public async Task<List<SearchableAlbum>> GetCutoffUnmetAlbumsAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default) =>
        await GetAllWantedAsync<SearchableAlbum>(arrInstance, "cutoff", monitored, cancellationToken);
}