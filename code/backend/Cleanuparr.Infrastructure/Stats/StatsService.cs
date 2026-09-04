using System.Text;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Service for aggregating application statistics
/// </summary>
public class StatsService : IStatsService
{
    private readonly ILogger<StatsService> _logger;
    private readonly EventsContext _eventsContext;
    private readonly IHealthCheckService _healthCheckService;
    private readonly IJobManagementService _jobManagementService;
    private readonly IDatabaseProvider _databaseProvider;

    public StatsService(
        ILogger<StatsService> logger,
        EventsContext eventsContext,
        IHealthCheckService healthCheckService,
        IJobManagementService jobManagementService,
        IDatabaseProvider databaseProvider)
    {
        _logger = logger;
        _eventsContext = eventsContext;
        _healthCheckService = healthCheckService;
        _jobManagementService = jobManagementService;
        _databaseProvider = databaseProvider;
    }

    private static readonly Dictionary<EventType, StrikeType> StrikeEventToType = new()
    {
        [EventType.StalledStrike] = StrikeType.Stalled,
        [EventType.DownloadingMetadataStrike] = StrikeType.DownloadingMetadata,
        [EventType.FailedImportStrike] = StrikeType.FailedImport,
        [EventType.SlowSpeedStrike] = StrikeType.SlowSpeed,
        [EventType.SlowTimeStrike] = StrikeType.SlowTime,
        [EventType.DeadTorrentStrike] = StrikeType.DeadTorrent,
    };

    private static readonly EventType[] StrikeEventTypes = [.. StrikeEventToType.Keys];

    private static readonly DeleteReason[] MalwareReasons =
    [
        DeleteReason.AllFilesBlocked,
        DeleteReason.AtLeastOneFileBlocked,
    ];

    /// <inheritdoc />
    public async Task<StatsV2Response> GetStatsV2Async(int hours, bool includeDryRun = false)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddHours(-hours);

        Dictionary<string, int> byType = await MergedCountsAsync(cutoff, e => e.EventType, includeDryRun);
        Dictionary<string, int> bySeverity = await MergedCountsAsync(cutoff, e => e.Severity, includeDryRun);

        Dictionary<string, int> strikesByType = [];
        foreach ((EventType eventType, StrikeType strikeType) in StrikeEventToType)
        {
            int count = byType.GetValueOrDefault(eventType.ToString(), 0);
            if (count > 0)
            {
                strikesByType[strikeType.ToString()] = count;
            }
        }

        return new StatsV2Response
        {
            Events = new EventV2Stats
            {
                Total = byType.Values.Sum(),
                ByType = byType,
                BySeverity = bySeverity,
            },
            Strikes = new StrikeV2Stats
            {
                Total = strikesByType.Values.Sum(),
                ByType = strikesByType,
                Recovered = byType.GetValueOrDefault(EventType.StrikeReset.ToString(), 0),
            },
            Removals = new RemovalsV2Stats
            {
                Total = byType.GetValueOrDefault(EventType.QueueItemDeleted.ToString(), 0),
                ByReason = await RemovalsByReasonAsync(cutoff, includeDryRun),
            },
            Cleaned = new CleanedV2Stats
            {
                Total = byType.GetValueOrDefault(EventType.DownloadCleaned.ToString(), 0),
                ByReason = await CleanedByReasonAsync(cutoff, includeDryRun),
            },
            Searches = await GetSearchStatsAsync(cutoff, byType, includeDryRun),
            Jobs = await GetJobV2StatsAsync(cutoff),
            Health = GetHealthStats(),
            TimeframeHours = hours,
            GeneratedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <inheritdoc />
    public async Task<List<TimelineBucketDto>> GetTimelineAsync(string metric, int hours, TimelineBucketSize? bucket = null, bool includeDryRun = false)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset cutoff = now.AddHours(-hours);
        TimelineBucketSize size = bucket ?? TimelineBucketing.DefaultFor(hours);

        Dictionary<DateTimeOffset, int> counts = await MetricCountsAsync(cutoff, metric, size, includeDryRun);

        List<TimelineBucketDto> series = [];
        foreach (DateTimeOffset point in TimelineBucketing.Buckets(cutoff, now, size))
        {
            series.Add(new TimelineBucketDto { Date = point, Count = counts.GetValueOrDefault(point) });
        }

        return series;
    }

    private async Task<Dictionary<string, int>> MergedCountsAsync<TKey>(
        DateTimeOffset cutoff,
        System.Linq.Expressions.Expression<Func<Persistence.Models.Events.AppEvent, TKey>> selector,
        bool includeDryRun)
        where TKey : notnull
    {
        var grouped = await _eventsContext.Events
            .Where(e => e.Timestamp >= cutoff && (includeDryRun || !e.IsDryRun))
            .GroupBy(selector)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();

        Dictionary<string, int> counts = [];
        foreach (var entry in grouped)
        {
            string key = entry.Key.ToString() ?? string.Empty;
            counts[key] = counts.GetValueOrDefault(key) + entry.Count;
        }

        return counts;
    }

    private async Task<Dictionary<string, int>> RemovalsByReasonAsync(DateTimeOffset cutoff, bool includeDryRun)
    {
        var grouped = await _eventsContext.Events
            .Where(e => e.Timestamp >= cutoff && (includeDryRun || !e.IsDryRun)
                && e.EventType == EventType.QueueItemDeleted
                && e.DeleteReason != null && e.DeleteReason != DeleteReason.None)
            .GroupBy(e => e.DeleteReason!.Value)
            .Select(g => new { Reason = g.Key, Count = g.Count() })
            .ToListAsync();

        return grouped.ToDictionary(x => x.Reason.ToString(), x => x.Count);
    }

    private async Task<Dictionary<string, int>> CleanedByReasonAsync(DateTimeOffset cutoff, bool includeDryRun)
    {
        var grouped = await _eventsContext.Events
            .Where(e => e.Timestamp >= cutoff && (includeDryRun || !e.IsDryRun)
                && e.EventType == EventType.DownloadCleaned
                && e.CleanReason != null && e.CleanReason != CleanReason.None)
            .GroupBy(e => e.CleanReason!.Value)
            .Select(g => new { Reason = g.Key, Count = g.Count() })
            .ToListAsync();

        return grouped.ToDictionary(x => x.Reason.ToString(), x => x.Count);
    }

    private async Task<SearchesV2Stats> GetSearchStatsAsync(DateTimeOffset cutoff, Dictionary<string, int> byType, bool includeDryRun)
    {
        var rows = await _eventsContext.Events
            .Where(e => e.Timestamp >= cutoff && (includeDryRun || !e.IsDryRun)
                && e.EventType == EventType.SearchTriggered)
            .Select(e => new { e.SearchStatus, e.SearchReason, GrabbedCount = e.GrabbedItems.Count })
            .ToListAsync();

        Dictionary<SearchCommandStatus, int> statusCounts = rows
            .Where(r => r.SearchStatus != null)
            .GroupBy(r => r.SearchStatus!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        Dictionary<string, int> byReason = rows
            .Where(r => r.SearchReason != null)
            .GroupBy(r => r.SearchReason!.Value)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        return new SearchesV2Stats
        {
            Total = byType.GetValueOrDefault(EventType.SearchTriggered.ToString(), 0),
            Completed = statusCounts.GetValueOrDefault(SearchCommandStatus.Completed, 0),
            Failed = statusCounts.GetValueOrDefault(SearchCommandStatus.Failed, 0)
                + statusCounts.GetValueOrDefault(SearchCommandStatus.TimedOut, 0),
            Grabbed = rows.Sum(r => r.GrabbedCount),
            ByReason = byReason,
        };
    }

    private async Task<Dictionary<DateTimeOffset, int>> MetricCountsAsync(DateTimeOffset cutoff, string metric, TimelineBucketSize size, bool includeDryRun)
    {
        EventType[]? types = metric switch
        {
            "strikesIssued" => StrikeEventTypes,
            "recovered" => [EventType.StrikeReset],
            "removed" => [EventType.QueueItemDeleted],
            "malwareBlocked" => [EventType.QueueItemDeleted],
            _ => null, // "events" or unknown → all types
        };
        bool malwareOnly = metric == "malwareBlocked";

        List<object> parameters = [cutoff];
        StringBuilder where = new("WHERE \"timestamp\" >= {0}");

        if (types is not null)
        {
            string placeholders = string.Join(", ", types.Select((_, i) => $"{{{parameters.Count + i}}}"));
            where.Append($" AND event_type IN ({placeholders})");
            parameters.AddRange(types.Select(t => (object)t.ToString().ToLowerInvariant()));
        }

        if (malwareOnly)
        {
            string placeholders = string.Join(", ", MalwareReasons.Select((_, i) => $"{{{parameters.Count + i}}}"));
            where.Append($" AND delete_reason IN ({placeholders})");
            parameters.AddRange(MalwareReasons.Select(r => (object)r.ToString().ToLowerInvariant()));
        }

        if (!includeDryRun)
        {
            where.Append($" AND is_dry_run = {{{parameters.Count}}}");
            parameters.Add(false);
        }

        string bucketExpr = _databaseProvider.GetTimelineBucketExpr(size);
        string? schema = _databaseProvider.GetSchema(DbContextKind.Events);
        string eventsTable = schema is null ? "events" : $"{schema}.events";
        string sql = $"""
            SELECT {bucketExpr} AS "bucket", COUNT(*) AS "count"
            FROM {eventsTable}
            {where}
            GROUP BY {bucketExpr}
            """;

        List<BucketCount> rows = await _eventsContext.Database
            .SqlQueryRaw<BucketCount>(sql, parameters.ToArray())
            .ToListAsync();

        return rows.ToDictionary(
            r => TimelineBucketing.ParseKey(r.Bucket, size),
            r => r.Count);
    }

    private sealed class BucketCount
    {
        public string Bucket { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private async Task<JobV2Stats> GetJobV2StatsAsync(DateTimeOffset cutoff)
    {
        var jobRuns = await _eventsContext.JobRuns
            .Where(j => j.StartedAt >= cutoff)
            .GroupBy(j => j.Type)
            .Select(g => new
            {
                Type = g.Key,
                TotalRuns = g.Count(),
                Completed = g.Count(j => j.Status == JobRunStatus.Completed),
                Failed = g.Count(j => j.Status == JobRunStatus.Failed),
                LastRunAt = g.Max(j => j.StartedAt),
            })
            .ToListAsync();

        Dictionary<string, JobTypeV2Stats> byType = jobRuns.ToDictionary(
            j => j.Type.ToString(),
            j => new JobTypeV2Stats
            {
                Total = j.TotalRuns,
                Completed = j.Completed,
                Failed = j.Failed,
                LastRunAt = j.LastRunAt,
            });

        var allJobs = await _jobManagementService.GetAllJobs();
        foreach (var job in allJobs)
        {
            if (byType.TryGetValue(job.JobType, out JobTypeV2Stats? stats))
            {
                stats.NextRunAt = job.NextRunTime;
            }
            else
            {
                byType[job.JobType] = new JobTypeV2Stats { NextRunAt = job.NextRunTime };
            }
        }

        return new JobV2Stats
        {
            Total = byType.Values.Sum(s => s.Total),
            Completed = byType.Values.Sum(s => s.Completed),
            Failed = byType.Values.Sum(s => s.Failed),
            ByType = byType,
        };
    }

    private HealthStats GetHealthStats()
    {
        var downloadClientHealth = _healthCheckService.GetAllClientHealth();
        var arrHealth = _healthCheckService.GetAllArrInstanceHealth();

        return new HealthStats
        {
            DownloadClients = downloadClientHealth.Values.Select(h => new DownloadClientHealthDto
            {
                Id = h.ClientId,
                Name = h.ClientName,
                Type = h.ClientTypeName.ToString(),
                IsHealthy = h.IsHealthy,
                LastChecked = h.LastChecked,
                ResponseTimeMs = h.ResponseTime.TotalMilliseconds,
                ErrorMessage = h.ErrorMessage
            }).ToList(),
            ArrInstances = arrHealth.Values.Select(h => new ArrInstanceHealthDto
            {
                Id = h.InstanceId,
                Name = h.InstanceName,
                Type = h.InstanceType.ToString(),
                IsHealthy = h.IsHealthy,
                LastChecked = h.LastChecked,
                ErrorMessage = h.ErrorMessage
            }).ToList()
        };
    }
}
