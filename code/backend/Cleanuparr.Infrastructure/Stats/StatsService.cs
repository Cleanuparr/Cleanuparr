using System.Globalization;
using System.Text;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
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

    public StatsService(
        ILogger<StatsService> logger,
        EventsContext eventsContext,
        IHealthCheckService healthCheckService,
        IJobManagementService jobManagementService)
    {
        _logger = logger;
        _eventsContext = eventsContext;
        _healthCheckService = healthCheckService;
        _jobManagementService = jobManagementService;
    }

    /// <inheritdoc />
    public async Task<StatsResponse> GetStatsAsync(int hours = 24, int includeEvents = 0, int includeStrikes = 0)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-hours);

        var eventStats = await GetEventStatsAsync(cutoff, hours, includeEvents);
        var strikeStats = await GetStrikeStatsAsync(cutoff, hours, includeStrikes);
        var jobStats = await GetJobStatsAsync(cutoff, hours);
        var healthStats = GetHealthStats();

        return new StatsResponse
        {
            Events = eventStats,
            Strikes = strikeStats,
            Jobs = jobStats,
            Health = healthStats,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    private static readonly EventType[] StrikeEventTypes =
    [
        EventType.StalledStrike,
        EventType.DownloadingMetadataStrike,
        EventType.FailedImportStrike,
        EventType.SlowSpeedStrike,
        EventType.SlowTimeStrike,
        EventType.DeadTorrentStrike,
    ];

    private static readonly DeleteReason[] MalwareReasons =
    [
        DeleteReason.AllFilesBlocked,
        DeleteReason.AtLeastOneFileBlocked,
    ];

    /// <inheritdoc />
    public async Task<StatsV2Response> GetStatsV2Async(int hours)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddHours(-hours);

        Dictionary<string, int> byType = await MergedCountsAsync(cutoff, e => e.EventType);
        Dictionary<string, int> bySeverity = await MergedCountsAsync(cutoff, e => e.Severity);

        Dictionary<string, int> activeStrikes = (await _eventsContext.Strikes
                .GroupBy(s => s.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync())
            .ToDictionary(x => x.Type.ToString(), x => x.Count);

        return new StatsV2Response
        {
            Events = new EventV2Stats
            {
                TotalCount = byType.Values.Sum(),
                ByType = byType,
                BySeverity = bySeverity,
            },
            Strikes = new StrikeV2Stats
            {
                Active = activeStrikes,
                Issued = StrikeEventTypes.Sum(t => byType.GetValueOrDefault(t.ToString(), 0)),
                Recovered = byType.GetValueOrDefault(EventType.StrikeReset.ToString(), 0),
                Removed = byType.GetValueOrDefault(EventType.QueueItemDeleted.ToString(), 0),
            },
            Malware = new MalwareV2Stats
            {
                Blocked = await CountMalwareAsync(cutoff),
            },
            Jobs = await GetJobV2StatsAsync(cutoff),
            Health = GetHealthStats(),
            GeneratedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <inheritdoc />
    public async Task<List<TimelineBucketDto>> GetTimelineAsync(string metric, int hours)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset cutoff = now.AddHours(-hours);
        bool hourly = TimelineBucketing.IsHourly(hours);

        Dictionary<DateTimeOffset, int> counts = await MetricCountsAsync(cutoff, metric, hourly);

        List<TimelineBucketDto> series = [];
        foreach (DateTimeOffset bucket in TimelineBucketing.Buckets(cutoff, now, hourly))
        {
            series.Add(new TimelineBucketDto { Date = bucket, Count = counts.GetValueOrDefault(bucket) });
        }

        return series;
    }

    private async Task<Dictionary<string, int>> MergedCountsAsync<TKey>(
        DateTimeOffset cutoff,
        System.Linq.Expressions.Expression<Func<Persistence.Models.Events.AppEvent, TKey>> selector)
        where TKey : notnull
    {
        var grouped = await _eventsContext.Events
            .Where(e => e.Timestamp >= cutoff)
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

    private async Task<int> CountMalwareAsync(DateTimeOffset cutoff)
    {
        return await _eventsContext.Events
            .CountAsync(e => e.Timestamp >= cutoff && e.EventType == EventType.QueueItemDeleted
                && e.DeleteReason != null && MalwareReasons.Contains(e.DeleteReason.Value));
    }

    private async Task<Dictionary<DateTimeOffset, int>> MetricCountsAsync(DateTimeOffset cutoff, string metric, bool hourly)
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

        List<object> parameters = [cutoff.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture)];
        StringBuilder where = new("WHERE timestamp >= {0}");

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

        string bucketExpr = $"substr(timestamp, 1, {TimelineBucketing.KeyLength(hourly)})";
        string sql = $"""
            SELECT {bucketExpr} AS "bucket", COUNT(*) AS "count"
            FROM events
            {where}
            GROUP BY {bucketExpr}
            """;

        List<BucketCount> rows = await _eventsContext.Database
            .SqlQueryRaw<BucketCount>(sql, parameters.ToArray())
            .ToListAsync();

        return rows.ToDictionary(
            r => TimelineBucketing.ParseKey(r.Bucket, hourly),
            r => r.Count);
    }

    private sealed class BucketCount
    {
        public string Bucket { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// Builds the per-job-type run stats for the window, enriched with each job's next scheduled run.
    /// Shared by the v1 and v2 job-stats projections.
    /// </summary>
    private async Task<Dictionary<string, JobTypeStats>> BuildJobTypeStatsAsync(DateTimeOffset cutoff)
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

        Dictionary<string, JobTypeStats> byType = jobRuns.ToDictionary(
            j => j.Type.ToString(),
            j => new JobTypeStats
            {
                TotalRuns = j.TotalRuns,
                Completed = j.Completed,
                Failed = j.Failed,
                LastRunAt = j.LastRunAt,
            });

        var allJobs = await _jobManagementService.GetAllJobs();
        foreach (var job in allJobs)
        {
            if (byType.TryGetValue(job.JobType, out JobTypeStats? stats))
            {
                stats.NextRunAt = job.NextRunTime;
            }
            else
            {
                byType[job.JobType] = new JobTypeStats { NextRunAt = job.NextRunTime };
            }
        }

        return byType;
    }

    private async Task<JobV2Stats> GetJobV2StatsAsync(DateTimeOffset cutoff)
    {
        Dictionary<string, JobTypeStats> byType = await BuildJobTypeStatsAsync(cutoff);

        return new JobV2Stats
        {
            TotalRuns = byType.Values.Sum(s => s.TotalRuns),
            Completed = byType.Values.Sum(s => s.Completed),
            Failed = byType.Values.Sum(s => s.Failed),
            ByType = byType,
        };
    }

    private async Task<EventStats> GetEventStatsAsync(DateTimeOffset cutoff, int hours, int includeEvents)
    {
        var eventsByType = await _eventsContext.Events
            .Where(e => e.Timestamp >= cutoff)
            .GroupBy(e => e.EventType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        var eventsBySeverity = await _eventsContext.Events
            .Where(e => e.Timestamp >= cutoff)
            .GroupBy(e => e.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync();

        var stats = new EventStats
        {
            TotalCount = eventsByType.Sum(e => e.Count),
            ByType = eventsByType.ToDictionary(e => e.Type.ToString(), e => e.Count),
            BySeverity = eventsBySeverity.ToDictionary(e => e.Severity.ToString(), e => e.Count),
            TimeframeHours = hours
        };

        if (includeEvents > 0)
        {
            stats.RecentItems = await _eventsContext.Events
                .Where(e => e.Timestamp >= cutoff)
                .OrderByDescending(e => e.Timestamp)
                .Take(includeEvents)
                .Select(e => new RecentEventDto
                {
                    Id = e.Id,
                    Timestamp = e.Timestamp,
                    EventType = e.EventType.ToString(),
                    Message = e.Message,
                    Severity = e.Severity.ToString(),
                })
                .ToListAsync();
        }

        return stats;
    }

    private async Task<StrikeStats> GetStrikeStatsAsync(DateTimeOffset cutoff, int hours, int includeStrikes)
    {
        var strikesByType = await _eventsContext.Strikes
            .Where(s => s.CreatedAt >= cutoff)
            .GroupBy(s => s.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        var itemsRemoved = await _eventsContext.DownloadItems
            .Where(d => d.IsRemoved && d.Strikes.Any(s => s.CreatedAt >= cutoff))
            .CountAsync();

        var stats = new StrikeStats
        {
            TotalCount = strikesByType.Sum(s => s.Count),
            ByType = strikesByType.ToDictionary(s => s.Type.ToString(), s => s.Count),
            ItemsRemoved = itemsRemoved,
            TimeframeHours = hours
        };

        if (includeStrikes > 0)
        {
            stats.RecentItems = await _eventsContext.Strikes
                .Include(s => s.DownloadItem)
                .Where(s => s.CreatedAt >= cutoff)
                .OrderByDescending(s => s.CreatedAt)
                .Take(includeStrikes)
                .Select(s => new RecentStrikeDto
                {
                    Id = s.Id,
                    Type = s.Type.ToString(),
                    CreatedAt = s.CreatedAt,
                    DownloadId = s.DownloadItem.DownloadId,
                    Title = s.DownloadItem.Title
                })
                .ToListAsync();
        }

        return stats;
    }

    private async Task<JobStats> GetJobStatsAsync(DateTimeOffset cutoff, int hours)
    {
        Dictionary<string, JobTypeStats> byType = await BuildJobTypeStatsAsync(cutoff);

        return new JobStats
        {
            ByType = byType,
            TimeframeHours = hours
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
