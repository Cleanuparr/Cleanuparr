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
    public async Task<StatsResponse> GetStatsAsync(int hours = 24)
    {
        var cutoff = DateTime.UtcNow.AddHours(-hours);

        var eventStats = await GetEventStatsAsync(cutoff, hours);
        var strikeStats = await GetStrikeStatsAsync(cutoff, hours);
        var jobStats = await GetJobStatsAsync(cutoff, hours);
        var healthStats = GetHealthStats();

        return new StatsResponse
        {
            Events = eventStats,
            Strikes = strikeStats,
            Jobs = jobStats,
            Health = healthStats,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<EventStats> GetEventStatsAsync(DateTime cutoff, int hours)
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

        return new EventStats
        {
            TotalCount = eventsByType.Sum(e => e.Count),
            ByType = eventsByType.ToDictionary(e => e.Type.ToString(), e => e.Count),
            BySeverity = eventsBySeverity.ToDictionary(e => e.Severity.ToString(), e => e.Count),
            TimeframeHours = hours
        };
    }

    private async Task<StrikeStats> GetStrikeStatsAsync(DateTime cutoff, int hours)
    {
        var strikesByType = await _eventsContext.Strikes
            .Where(s => s.CreatedAt >= cutoff)
            .GroupBy(s => s.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        var itemsRemoved = await _eventsContext.DownloadItems
            .Where(d => d.IsRemoved && d.Strikes.Any(s => s.CreatedAt >= cutoff))
            .CountAsync();

        return new StrikeStats
        {
            TotalCount = strikesByType.Sum(s => s.Count),
            ByType = strikesByType.ToDictionary(s => s.Type.ToString(), s => s.Count),
            ItemsRemoved = itemsRemoved,
            TimeframeHours = hours
        };
    }

    private async Task<JobStats> GetJobStatsAsync(DateTime cutoff, int hours)
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
                LastRunAt = g.Max(j => j.StartedAt)
            })
            .ToListAsync();

        var byType = jobRuns.ToDictionary(
            j => j.Type.ToString(),
            j => new JobTypeStats
            {
                TotalRuns = j.TotalRuns,
                Completed = j.Completed,
                Failed = j.Failed,
                LastRunAt = j.LastRunAt
            });

        var allJobs = await _jobManagementService.GetAllJobs();
        foreach (var job in allJobs)
        {
            if (byType.TryGetValue(job.JobType, out var stats))
            {
                stats.NextRunAt = job.NextRunTime;
            }
            else
            {
                byType[job.JobType] = new JobTypeStats { NextRunAt = job.NextRunTime };
            }
        }

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
