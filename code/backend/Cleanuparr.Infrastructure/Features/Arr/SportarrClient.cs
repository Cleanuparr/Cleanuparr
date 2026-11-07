using System.Globalization;
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

    /// <summary>
    /// Largest page size Sportarr's history accepts; anything above falls back to 20.
    /// </summary>
    private const int HistoryPageSize = 500;

    public SportarrClient(
        ILogger<SportarrClient> logger,
        IHttpClientFactory httpClientFactory,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor
    ) : base(logger, httpClientFactory, striker, dryRunInterceptor)
    {
    }

    /// <inheritdoc/>
    public override bool SupportsForceImport => false;

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

                    long taskId = queued?.TaskId
                        ?? throw new InvalidOperationException($"Sportarr queued no task for event {item.Id}");
                    taskIds.Add(taskId);
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

    /// <inheritdoc/>
    public override async Task<List<SearchableEpisode>> GetMissingEpisodesAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default)
    {
        // The native list holds monitored events only
        if (!monitored)
        {
            return [];
        }

        List<SportarrEvent> events = await GetAllPagesAsync<SportarrMissingResponse, SportarrEvent>(
            page =>
            {
                UriBuilder uriBuilder = new(arrInstance.Url);
                uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/wanted/missing";
                uriBuilder.Query = $"page={page}&pageSize={WantedPageSize}";
                return uriBuilder.Uri;
            },
            arrInstance.ApiKey,
            response => response.Events,
            response => response.TotalRecords,
            "missing",
            cancellationToken
        );

        return events.Select(ToSearchableEpisode).ToList();
    }

    /// <inheritdoc/>
    public override async Task<List<SearchableEpisode>> GetCutoffUnmetEpisodesAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default)
    {
        // The cutoff list ignores the monitored filter and holds monitored events only
        if (!monitored)
        {
            return [];
        }

        return await base.GetCutoffUnmetEpisodesAsync(arrInstance, monitored, cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task<HashSet<(long SeriesId, long SeasonNumber, long EpisodeId)>> GetRecentlyGrabbedEpisodesAsync(ArrInstance arrInstance, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        HashSet<(long SeriesId, long SeasonNumber, long EpisodeId)> grabbed = [];

        // History has no since filter, so read newest first and stop at the first older row
        int page = 1;
        int pageCount;

        do
        {
            UriBuilder uriBuilder = new(arrInstance.Url);
            uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/{ApiVersion}/history";
            uriBuilder.Query = $"page={page}&pageSize={HistoryPageSize}&sortDirection=descending";

            using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
            SetApiKey(request, arrInstance.ApiKey);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                _logger.LogError("grab history failed | {Uri}", uriBuilder.Uri);
                throw;
            }

            SportarrHistoryResponse? history = await DeserializeStreamAsync<SportarrHistoryResponse>(response, cancellationToken);

            if (history is null || history.Records.Count is 0)
            {
                break;
            }

            foreach (SportarrHistoryRecord record in history.Records)
            {
                if (DateTimeOffset.TryParse(record.Date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset date) && date < since)
                {
                    return grabbed;
                }

                if (record.EpisodeId > 0 && record.EventType.Equals("grabbed", StringComparison.OrdinalIgnoreCase))
                {
                    grabbed.Add((0, 0, record.EpisodeId));
                }
            }

            pageCount = history.Records.Count;
            page++;
        }
        while (pageCount == HistoryPageSize);

        return grabbed;
    }

    private static SearchableEpisode ToSearchableEpisode(SportarrEvent sportarrEvent)
    {
        DateTimeOffset? eventDate = DateTimeOffset.TryParse(sportarrEvent.EventDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)
            ? parsed
            : null;

        return new SearchableEpisode
        {
            Id = sportarrEvent.Id,
            SeriesId = sportarrEvent.LeagueId,
            SeasonNumber = sportarrEvent.SeasonNumber ?? eventDate?.UtcDateTime.Year ?? 0,
            EpisodeNumber = sportarrEvent.EpisodeNumber ?? 0,
            Title = sportarrEvent.Title,
            AirDateUtc = eventDate,
            Monitored = sportarrEvent.Monitored,
            HasFile = sportarrEvent.HasFile,
            Unsearchable = IsUnsearchableStatus(sportarrEvent.Status),
        };
    }

    // Matches Sportarr's own automatic search, which also accepts the US spelling
    private static bool IsUnsearchableStatus(string? status) =>
        status is not null
        && (status.Equals("Postponed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Canceled", StringComparison.OrdinalIgnoreCase));

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
