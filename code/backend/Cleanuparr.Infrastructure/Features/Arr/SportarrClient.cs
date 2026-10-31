using System.Text;
using System.Text.Json;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Sportarr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Json;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Arr;

public class SportarrClient : SonarrClient, ISportarrClient
{
    /// <summary>
    /// Page size used when listing Sportarr's tasks, newest first.
    /// </summary>
    private const int TaskPageSize = 500;

    public SportarrClient(
        ILogger<SportarrClient> logger,
        IHttpClientFactory httpClientFactory,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor
    ) : base(logger, httpClientFactory, striker, dryRunInterceptor)
    {
    }

    /// <inheritdoc/>
    public override async Task<List<long>> SearchItemsAsync(ArrInstance arrInstance, HashSet<SearchItem>? items)
    {
        if (items?.Count is null or 0)
        {
            return [];
        }

        List<long> taskIds = [];

        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/search/queue";

        foreach (SeriesSearchItem item in items.Cast<SeriesSearchItem>())
        {
            if (item.SearchType is not SeriesSearchType.Episode)
            {
                throw new ArgumentOutOfRangeException(nameof(items), item.SearchType, "Sportarr searches events only");
            }

            using HttpRequestMessage request = new(HttpMethod.Post, uriBuilder.Uri);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new SportarrSearchRequest { EventId = item.Id }, CleanuparrJsonOptions.Outbound),
                Encoding.UTF8,
                "application/json"
            );
            SetApiKey(request, arrInstance.ApiKey);

            try
            {
                using HttpResponseMessage? response = await _dryRunInterceptor.InterceptAsync(() => SendRequestAsync(request));

                if (response is not null)
                {
                    SportarrSearchQueueItem? queued = await DeserializeStreamAsync<SportarrSearchQueueItem>(response);

                    if (queued?.TaskId is { } taskId)
                    {
                        taskIds.Add(taskId);
                    }
                }

                _logger.LogInformation("events search triggered | {Url} | event id: {EventId}", arrInstance.Url, item.Id);
            }
            catch
            {
                _logger.LogError("events search failed | {Url} | event id: {EventId}", arrInstance.Url, item.Id);
                throw;
            }
        }

        return taskIds;
    }

    /// <inheritdoc/>
    public override async Task<ArrCommandStatus> GetCommandStatusAsync(ArrInstance arrInstance, long commandId)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/task/{commandId}";

        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        SportarrTask? task = await DeserializeStreamAsync<SportarrTask>(response);

        return task is null ? new ArrCommandStatus(commandId, ArrCommandState.Unknown, null) : ToCommandStatus(task);
    }

    /// <inheritdoc/>
    public override async Task<List<ArrCommandStatus>> GetCommandsAsync(ArrInstance arrInstance)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/task";
        uriBuilder.Query = $"pageSize={TaskPageSize}";

        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        List<SportarrTask>? tasks = await DeserializeStreamAsync<List<SportarrTask>>(response);

        return tasks?.Select(ToCommandStatus).ToList() ?? [];
    }

    private static ArrCommandStatus ToCommandStatus(SportarrTask task)
    {
        // Sportarr fails a search that found nothing to grab, and sets an exception only on a real error
        ArrCommandState state = task.Status switch
        {
            SportarrTaskStatus.Queued => ArrCommandState.Queued,
            SportarrTaskStatus.Running => ArrCommandState.Started,
            SportarrTaskStatus.Completed => ArrCommandState.Completed,
            SportarrTaskStatus.Failed when string.IsNullOrEmpty(task.Exception) => ArrCommandState.Completed,
            SportarrTaskStatus.Failed or SportarrTaskStatus.Cancelled or SportarrTaskStatus.Aborting => ArrCommandState.Failed,
            _ => ArrCommandState.Unknown,
        };

        return new ArrCommandStatus(task.Id, state, task.Exception);
    }
}
