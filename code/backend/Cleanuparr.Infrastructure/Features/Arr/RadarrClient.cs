using System.Text;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Entities.Radarr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Data.Models.Arr;
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

    public override async Task SearchItemsAsync(ArrInstance arrInstance, HashSet<SearchItem>? items)
    {
        if (items?.Count is null or 0)
        {
            return;
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
            response?.Dispose();
            
            _logger.LogInformation("{log}", GetSearchLog(arrInstance.Url, command, true, logContext));
        }
        catch
        {
            _logger.LogError("{log}", GetSearchLog(arrInstance.Url, command, false, logContext));
            throw;
        }
    }

    public override bool IsRecordValid(QueueRecord record)
    {
        if (record.MovieId is 0)
        {
            _logger.LogDebug("skip | movie id missing | {title}", record.Title);
            return false;
        }
        
        return base.IsRecordValid(record);
    }

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

    private async Task<Movie?> GetMovie(ArrInstance arrInstance, long movieId)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/v3/movie/{movieId}";
        
        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
                
        string responseBody = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<Movie>(responseBody);
    }
}