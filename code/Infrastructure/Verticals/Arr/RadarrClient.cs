﻿using System.Text;
using Common.Configuration.Arr;
using Common.Configuration.Logging;
using Domain.Models.Arr;
using Domain.Models.Radarr;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Infrastructure.Verticals.Arr;

public sealed class RadarrClient : ArrClient
{
    public RadarrClient(
        ILogger<ArrClient> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<LoggingConfig> loggingConfig,
        IMemoryCache cache
    ) : base(logger, httpClientFactory, loggingConfig, cache)
    {
    }

    protected override string GetQueueUrlPath(int page)
    {
        return $"/api/v3/queue?page={page}&pageSize=200&includeUnknownMovieItems=true&includeMovie=true";
    }

    public override async Task RefreshItemsAsync(ArrInstance arrInstance, ArrConfig config, HashSet<SearchItem>? items)
    {
        if (items?.Count is null or 0)
        {
            return;
        }

        List<long> ids = items.Select(item => item.Id).ToList();
        
        Uri uri = new(arrInstance.Url, "/api/v3/command");
        RadarrCommand command = new()
        {
            Name = "MoviesSearch",
            MovieIds = ids,
        };
        
        using HttpRequestMessage request = new(HttpMethod.Post, uri);
        request.Content = new StringContent(
            JsonConvert.SerializeObject(command),
            Encoding.UTF8,
            "application/json"
        );
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        string? logContext = await ComputeCommandLogContextAsync(arrInstance, command);

        try
        {
            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("{log}", GetSearchLog(arrInstance.Url, command, true, logContext));
        }
        catch
        {
            _logger.LogError("{log}", GetSearchLog(arrInstance.Url, command, false, logContext));
            throw;
        }
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
            if (!_loggingConfig.Enhanced)
            {
                return null;
            }

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
        Uri uri = new(arrInstance.Url, $"api/v3/movie/{movieId}");
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
                
        string responseBody = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<Movie>(responseBody);
    }
}