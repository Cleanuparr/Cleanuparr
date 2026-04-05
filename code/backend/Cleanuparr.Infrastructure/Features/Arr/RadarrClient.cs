using System.Text;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Entities.Radarr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Cleanuparr.Infrastructure.Features.Arr;

public class RadarrClient : ArrClient, IRadarrClient
{
    public RadarrClient(
        ILogger<ArrClient> logger,
        IHttpClientFactory httpClientFactory,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor
    ) : base(logger, httpClientFactory, striker, dryRunInterceptor)
    {
    }
    
    protected override string GetSystemStatusUrlPath()
    {
        return "/api/v3/system/status";
    }

    protected override string GetQueueUrlPath()
    {
        return "/api/v3/queue";
    }

    protected override string GetQueueUrlQuery(int page)
    {
        return $"page={page}&pageSize=200&includeUnknownMovieItems=true&includeMovie=true";
    }

    protected override string GetQueueDeleteUrlPath(long recordId)
    {
        return $"/api/v3/queue/{recordId}";
    }

    protected override string GetQueueDeleteUrlQuery(bool removeFromClient)
    {
        string query = "blocklist=true&skipRedownload=true&changeCategory=false";
        query += removeFromClient ? "&removeFromClient=true" : "&removeFromClient=false";

        return query;
    }

    public override async Task<List<long>> SearchItemsAsync(ArrInstance arrInstance, HashSet<SearchItem>? items)
    {
        if (items?.Count is null or 0)
        {
            return [];
        }

        List<long> ids = items.Select(item => item.Id).ToList();

        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/v3/command";

        RadarrCommand command = new()
        {
            Name = "MoviesSearch",
            MovieIds = ids,
        };

        using HttpRequestMessage request = new(HttpMethod.Post, uriBuilder.Uri);
        request.Content = new StringContent(
            JsonConvert.SerializeObject(command),
            Encoding.UTF8,
            "application/json"
        );
        SetApiKey(request, arrInstance.ApiKey);

        string? logContext = await ComputeCommandLogContextAsync(arrInstance, command);

        try
        {
            HttpResponseMessage? response = await _dryRunInterceptor.InterceptAsync<HttpResponseMessage>(SendRequestAsync, request);

            if (response is null)
            {
                return [];
            }

            long? commandId = await ReadCommandIdAsync(response);
            response.Dispose();

            _logger.LogInformation("{log}", GetSearchLog(arrInstance.Url, command, true, logContext));

            return commandId.HasValue ? [commandId.Value] : [];
        }
        catch
        {
            _logger.LogError("{log}", GetSearchLog(arrInstance.Url, command, false, logContext));
            throw;
        }
    }

    public override bool HasContentId(QueueRecord record) => record.MovieId is not 0;

    private static string GetSearchLog(Uri instanceUrl, RadarrCommand command, bool success, string? logContext)
    {
        string status = success ? "triggered" : "failed";
        string message = logContext ?? $"movie ids: {string.Join(',', command.MovieIds)}";

        return $"movie search {status} | {instanceUrl} | {message}";
    }

    private async Task<string?> ComputeCommandLogContextAsync(ArrInstance arrInstance, RadarrCommand command)
    {
        try
        {
            StringBuilder log = new();

            foreach (long movieId in command.MovieIds)
            {
                Movie? movie = await GetMovie(arrInstance, movieId);

                if (movie is null)
                {
                    return null;
                }

                log.Append($"[{movie.Title}]");
            }

            return log.ToString();
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "failed to compute log context");
        }

        return null;
    }

    public async Task<List<SearchableMovie>> GetAllMoviesAsync(ArrInstance arrInstance)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/v3/movie";

        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using Stream stream = await response.Content.ReadAsStreamAsync();
        using StreamReader sr = new(stream);
        using JsonTextReader reader = new(sr);
        JsonSerializer serializer = JsonSerializer.CreateDefault();
        return serializer.Deserialize<List<SearchableMovie>>(reader) ?? [];
    }
    
    public override async Task<List<Tag>> GetAllTagsAsync(ArrInstance arrInstance)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/v3/tag";
        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);
        
        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        
        using Stream stream = await response.Content.ReadAsStreamAsync();
        using StreamReader sr = new(stream);
        using JsonTextReader reader = new(sr);
        JsonSerializer serializer = JsonSerializer.CreateDefault();
        return serializer.Deserialize<List<Tag>>(reader) ?? [];
    }

    public async Task<List<ArrQualityProfile>> GetQualityProfilesAsync(ArrInstance arrInstance)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/v3/qualityprofile";

        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        return await DeserializeStreamAsync<List<ArrQualityProfile>>(response) ?? [];
    }

    public async Task<Dictionary<long, int>> GetMovieFileScoresAsync(ArrInstance arrInstance, List<long> movieFileIds)
    {
        Dictionary<long, int> scores = new();

        // Batch in chunks of 100 to avoid 414 URI Too Long
        foreach (long[] batch in movieFileIds.Chunk(100))
        {
            UriBuilder uriBuilder = new(arrInstance.Url);
            uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/v3/moviefile";
            uriBuilder.Query = string.Join('&', batch.Select(id => $"movieFileIds={id}"));

            using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
            SetApiKey(request, arrInstance.ApiKey);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            List<MediaFileScore> files = await DeserializeStreamAsync<List<MediaFileScore>>(response) ?? [];

            foreach (MediaFileScore file in files)
            {
                scores[file.Id] = file.CustomFormatScore;
            }
        }

        return scores;
    }

    private async Task<Movie?> GetMovie(ArrInstance arrInstance, long movieId)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/v3/movie/{movieId}";

        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        return await DeserializeStreamAsync<Movie>(response);
    }
}