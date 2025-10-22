using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Cleanuparr.Shared.Helpers;
using Data.Models.Arr;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Cleanuparr.Infrastructure.Features.Arr;

public abstract class ArrClient : IArrClient
{
    protected readonly ILogger<ArrClient> _logger;
    protected readonly HttpClient _httpClient;
    protected readonly IStriker _striker;
    protected readonly IDryRunInterceptor _dryRunInterceptor;
    
    protected ArrClient(
        ILogger<ArrClient> logger,
        IHttpClientFactory httpClientFactory,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor
    )
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);
        _striker = striker;
        _dryRunInterceptor = dryRunInterceptor;
    }

    public virtual async Task<QueueListResponse> GetQueueItemsAsync(ArrInstance arrInstance, int page)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/{GetQueueUrlPath().TrimStart('/')}";
        uriBuilder.Query = GetQueueUrlQuery(page);

        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);
        
        using HttpResponseMessage response = await _httpClient.SendAsync(request);

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            _logger.LogError("queue list failed | {uri}", uriBuilder.Uri);
            throw;
        }
        
        string responseBody = await response.Content.ReadAsStringAsync();
        QueueListResponse? queueResponse = JsonConvert.DeserializeObject<QueueListResponse>(responseBody);

        if (queueResponse is null)
        {
            throw new Exception($"unrecognized queue list response | {uriBuilder.Uri} | {responseBody}");
        }

        return queueResponse;
    }

    public virtual async Task<bool> ShouldRemoveFromQueue(InstanceType instanceType, QueueRecord record, bool isPrivateDownload, short arrMaxStrikes)
    {
        var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>();
        
        if (queueCleanerConfig.FailedImport.IgnorePrivate && isPrivateDownload)
        {
            // ignore private trackers
            _logger.LogDebug("skip failed import check | download is private | {name}", record.Title);
            return false;
        }
        
        bool hasWarn() => record.TrackedDownloadStatus
            .Equals("warning", StringComparison.InvariantCultureIgnoreCase);
        bool isImportBlocked() => record.TrackedDownloadState
            .Equals("importBlocked", StringComparison.InvariantCultureIgnoreCase);
        bool isImportPending() => record.TrackedDownloadState
            .Equals("importPending", StringComparison.InvariantCultureIgnoreCase);
        bool isImportFailed() => record.TrackedDownloadState
            .Equals("importFailed", StringComparison.InvariantCultureIgnoreCase);
        bool isFailedLidarr() => instanceType is InstanceType.Lidarr &&
                                 (record.Status.Equals("failed", StringComparison.InvariantCultureIgnoreCase) ||
                                  record.Status.Equals("completed", StringComparison.InvariantCultureIgnoreCase)) &&
                                 hasWarn();
        
        if (hasWarn() && (isImportBlocked() || isImportPending() || isImportFailed()) || isFailedLidarr())
        {
            if (!ShouldStrikeFailedImport(queueCleanerConfig, record))
            {
                return false;
            }

            if (arrMaxStrikes is 0)
            {
                _logger.LogDebug("skip failed import check | arr max strikes is 0 | {name}", record.Title);
                return false;
            }
            
            ushort maxStrikes = arrMaxStrikes > 0 ? (ushort)arrMaxStrikes : queueCleanerConfig.FailedImport.MaxStrikes;
            
            _logger.LogInformation(
                "Item {title} has failed import status with the following reason(s):\n{messages}",
                record.Title,
                string.Join("\n",  record.StatusMessages?.Select(JsonConvert.SerializeObject) ?? [])
            );
            
            return await _striker.StrikeAndCheckLimit(
                record.DownloadId,
                record.Title,
                maxStrikes,
                StrikeType.FailedImport
            );
        }

        return false;
    }
    
    public virtual async Task DeleteQueueItemAsync(
        ArrInstance arrInstance,
        QueueRecord record,
        bool removeFromClient,
        DeleteReason deleteReason
    )
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/{GetQueueDeleteUrlPath(record.Id).TrimStart('/')}";
        uriBuilder.Query = GetQueueDeleteUrlQuery(removeFromClient);

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Delete, uriBuilder.Uri);
            SetApiKey(request, arrInstance.ApiKey);

            HttpResponseMessage? response = await _dryRunInterceptor.InterceptAsync<HttpResponseMessage>(SendRequestAsync, request);
            response?.Dispose();
            
            _logger.LogInformation(
                removeFromClient
                    ? "queue item deleted with reason {reason} | {url} | {title}"
                    : "queue item removed from arr with reason {reason} | {url} | {title}",
                deleteReason.ToString(),
                arrInstance.Url,
                record.Title
            );
        }
        catch
        {
            _logger.LogError("queue delete failed | {uri} | {title}", uriBuilder.Uri, record.Title);
            throw;
        }
    }

    public abstract Task SearchItemsAsync(ArrInstance arrInstance, HashSet<SearchItem>? items);

    public virtual bool IsRecordValid(QueueRecord record)
    {
        if (string.IsNullOrEmpty(record.DownloadId))
        {
            _logger.LogDebug("skip | download id is null for {title}", record.Title);
            return false;
        }

        return true;
    }
    
    /// <summary>
    /// Tests the connection to an Arr instance
    /// </summary>
    /// <param name="arrInstance">The instance to test connection to</param>
    /// <returns>Task that completes when the connection test is done</returns>
    public virtual async Task TestConnectionAsync(ArrInstance arrInstance)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/v3/system/status";

        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);
        
        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        
        response.EnsureSuccessStatusCode();
        
        _logger.LogDebug("Connection test successful for {url}", arrInstance.Url);
    }
    
    protected abstract string GetQueueUrlPath();

    protected abstract string GetQueueUrlQuery(int page);

    protected abstract string GetQueueDeleteUrlPath(long recordId);
    
    protected abstract string GetQueueDeleteUrlQuery(bool removeFromClient);
    
    protected virtual void SetApiKey(HttpRequestMessage request, string apiKey)
    {
        request.Headers.Add("x-api-key", apiKey);
    }

    protected virtual async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request)
    {
        HttpResponseMessage response = await _httpClient.SendAsync(request);
        
        response.EnsureSuccessStatusCode();
        
        return response;
    }
    
    /// <summary>
    /// Determines whether the failed import record should be skipped
    /// </summary>
    private bool ShouldStrikeFailedImport(QueueCleanerConfig queueCleanerConfig, QueueRecord record)
    {
        if (record.StatusMessages?.Count is null or 0)
        {
            _logger.LogWarning("skip failed import check | no status message found | {name}", record.Title);
            return false;
        }
        
        HashSet<string> messages = record.StatusMessages
            .SelectMany(x => x.Messages ?? Enumerable.Empty<string>())
            .ToHashSet();
        record.StatusMessages.Select(x => x.Title)
            .ToList()
            .ForEach(x => messages.Add(x));
        
        var patterns = queueCleanerConfig.FailedImport.Patterns;
        var patternMode = queueCleanerConfig.FailedImport.PatternMode;
        
        var matched = messages.Any(
            m => patterns.Any(
                p => !string.IsNullOrWhiteSpace(p?.Trim()) && m.Contains(p, StringComparison.InvariantCultureIgnoreCase)
            )
        );

        if (patternMode is PatternMode.Exclude && matched)
        {
            // contains an excluded/ignored pattern -> skip
            _logger.LogTrace("skip failed import check | excluded pattern matched | {name}", record.Title);
            return false;
        }

        if (patternMode is PatternMode.Include && (!matched || patterns.Count is 0))
        {
            // does not match any included patterns -> skip
            _logger.LogTrace("skip failed import check | no included pattern matched | {name}", record.Title);
            return false;
        }
        
        return true;
    }
}