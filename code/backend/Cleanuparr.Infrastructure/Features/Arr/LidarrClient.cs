using System.Text;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Entities.Lidarr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Data.Models.Arr;
using Infrastructure.Interceptors;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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

    protected override string GetQueueUrlPath()
    {
        return "/api/v1/queue";
    }

    protected override string GetQueueUrlQuery(int page)
    {
        return $"page={page}&pageSize=200&includeUnknownArtistItems=true&includeArtist=true&includeAlbum=true";
    }

    protected override string GetQueueDeleteUrlPath(long recordId)
    {
        return $"/api/v1/queue/{recordId}";
    }

    protected override string GetQueueDeleteUrlQuery(bool removeFromClient)
    {
        string query = "blocklist=true&skipRedownload=true&changeCategory=false";
        query += removeFromClient ? "&removeFromClient=true" : "&removeFromClient=false";

        return query;
    }

    public override async Task SearchItemsAsync(ArrInstance arrInstance, HashSet<SearchItem>? items)
    {
        if (items?.Count is null or 0)
        {
            return;
        }

        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/v1/command";

        foreach (var command in GetSearchCommands(items))
        {
            using HttpRequestMessage request = new(HttpMethod.Post, uriBuilder.Uri);
            request.Content = new StringContent(
                JsonConvert.SerializeObject(command, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                Encoding.UTF8,
                "application/json"
            );
            SetApiKey(request, arrInstance.ApiKey);

            string? logContext = await ComputeCommandLogContextAsync(arrInstance, command);

            try
            {
                HttpResponseMessage? response = await _dryRunInterceptor.InterceptAsync<HttpResponseMessage>(SendRequestAsync, request);
                response?.Dispose();
                
                _logger.LogInformation("{log}", GetSearchLog(arrInstance.Url, command, true, logContext));
            }
            catch
            {
                _logger.LogError("{log}", GetSearchLog(arrInstance.Url, command, false, logContext));
                throw;
            }
        }
    }

    public override bool IsRecordValid(QueueRecord record)
    {
        if (record.ArtistId is 0 || record.AlbumId is 0)
        {
            _logger.LogDebug("skip | artist id and/or album id missing | {title}", record.Title);
            return false;
        }

        return base.IsRecordValid(record);
    }

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
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/v1/album";
        uriBuilder.Query = string.Join('&', albumIds.Select(x => $"albumIds={x}"));
        
        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        string responseBody = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<List<Album>>(responseBody);
    }

    private List<LidarrCommand> GetSearchCommands(HashSet<SearchItem> items)
    {
        const string albumSearch = "AlbumSearch";

        return [new LidarrCommand { Name = albumSearch, AlbumIds = items.Select(i => i.Id).ToList() }];
    }
}