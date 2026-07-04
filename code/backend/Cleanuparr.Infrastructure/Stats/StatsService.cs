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
    public async Task<StatsV2Response> GetStatsV2Async(int hours, string window)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddHours(-hours);

        Dictionary<string, int> byType = await MergedCountsAsync(cutoff, e => e.EventType, h => h.EventType);
        Dictionary<string, int> bySeverity = await MergedCountsAsync(cutoff, e => e.Severity, h => h.Severity);

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
                // byType already holds the merged (active + history) per-type counts for this cutoff.
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
            Window = window,
            GeneratedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <inheritdoc />
    public async Task<List<TimelineBucketDto>> GetTimelineAsync(string metric, int hours)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddHours(-hours);

        List<DateTimeOffset> timestamps = await MetricTimestampsAsync(cutoff, metric);

        Dictionary<DateOnly, int> counts = timestamps
            .GroupBy(t => DateOnly.FromDateTime(t.UtcDateTime))
            .ToDictionary(g => g.Key, g => g.Count());

        // Fill every day in the window so the series is continuous for charting.
        List<TimelineBucketDto> series = [];
        DateOnly start = DateOnly.FromDateTime(cutoff.UtcDateTime);
        DateOnly end = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        for (DateOnly day = start; day <= end; day = day.AddDays(1))
        {
            series.Add(new TimelineBucketDto { Date = day, Count = counts.GetValueOrDefault(day) });
        }

        return series;
    }

    private async Task<Dictionary<string, int>> MergedCountsAsync<TKey>(
        DateTimeOffset cutoff,
        System.Linq.Expressions.Expression<Func<Persistence.Models.Events.AppEvent, TKey>> activeSelector,
        System.Linq.Expressions.Expression<Func<Persistence.Models.Events.EventHistory, TKey>> historySelector)
        where TKey : notnull
    {
        var active = await _eventsContext.Events
            .Where(e => e.Timestamp >= cutoff)
            .GroupBy(activeSelector)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();

        var history = await _eventsContext.EventHistory
            .Where(e => e.Timestamp >= cutoff)
            .GroupBy(historySelector)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();

        Dictionary<string, int> merged = [];
        foreach (var entry in active.Concat(history))
        {
            string key = entry.Key.ToString() ?? string.Empty;
            merged[key] = merged.GetValueOrDefault(key) + entry.Count;
        }

        return merged;
    }

    private async Task<int> CountMalwareAsync(DateTimeOffset cutoff)
    {
        int active = await _eventsContext.Events
            .CountAsync(e => e.Timestamp >= cutoff && e.EventType == EventType.QueueItemDeleted
                && e.DeleteReason != null && MalwareReasons.Contains(e.DeleteReason.Value));
        int history = await _eventsContext.EventHistory
            .CountAsync(e => e.Timestamp >= cutoff && e.EventType == EventType.QueueItemDeleted
                && e.DeleteReason != null && MalwareReasons.Contains(e.DeleteReason.Value));
        return active + history;
    }

    private async Task<List<DateTimeOffset>> MetricTimestampsAsync(DateTimeOffset cutoff, string metric)
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

        IQueryable<Persistence.Models.Events.AppEvent> active = _eventsContext.Events.Where(e => e.Timestamp >= cutoff);
        IQueryable<Persistence.Models.Events.EventHistory> history = _eventsContext.EventHistory.Where(e => e.Timestamp >= cutoff);

        if (types is not null)
        {
            active = active.Where(e => types.Contains(e.EventType));
            history = history.Where(e => types.Contains(e.EventType));
        }

        if (malwareOnly)
        {
            active = active.Where(e => e.DeleteReason != null && MalwareReasons.Contains(e.DeleteReason.Value));
            history = history.Where(e => e.DeleteReason != null && MalwareReasons.Contains(e.DeleteReason.Value));
        }

        // ponytail: windows are bounded (max ~1y of low-volume events), so bucket in memory rather than push a date expression to SQLite.
        List<DateTimeOffset> timestamps = await active.Select(e => e.Timestamp).ToListAsync();
        timestamps.AddRange(await history.Select(e => e.Timestamp).ToListAsync());
        return timestamps;
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
