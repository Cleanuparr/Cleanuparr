using Cleanuparr.Api.Features.Seeker.Contracts.Responses;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.State;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.Seeker.Controllers;

[ApiController]
[Route("api/seeker/cf-scores")]
[Authorize]
public sealed class CustomFormatScoreController : ControllerBase
{
    private readonly DataContext _dataContext;

    public CustomFormatScoreController(DataContext dataContext)
    {
        _dataContext = dataContext;
    }

    /// <summary>
    /// Gets current CF scores with pagination, optionally filtered by instance.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCustomFormatScores(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? instanceId = null,
        [FromQuery] string? search = null,
        [FromQuery] CfScoresSortBy sortBy = CfScoresSortBy.Title,
        [FromQuery] SortDirection? sortDirection = null,
        [FromQuery] string? qualityProfile = null,
        [FromQuery] InstanceType? itemType = null,
        [FromQuery] CutoffFilter cutoffFilter = CutoffFilter.All,
        [FromQuery] MonitoredFilter monitoredFilter = MonitoredFilter.All)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 50;
        }

        if (pageSize > 500)
        {
            pageSize = 500;
        }

        var query = _dataContext.CustomFormatScoreEntries
            .AsNoTracking()
            .AsQueryable();

        if (instanceId.HasValue)
        {
            query = query.Where(e => e.ArrInstanceId == instanceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e => e.Title.ToLower().Contains(search.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(qualityProfile))
        {
            query = query.Where(e => e.QualityProfileName == qualityProfile);
        }

        if (itemType.HasValue)
        {
            InstanceType typeValue = itemType.Value;
            query = query.Where(e => e.ItemType == typeValue);
        }

        switch (cutoffFilter)
        {
            case CutoffFilter.Below:
                query = query.Where(e => e.CurrentScore < e.CutoffScore);
                break;
            case CutoffFilter.Met:
                query = query.Where(e => e.CurrentScore >= e.CutoffScore);
                break;
        }

        switch (monitoredFilter)
        {
            case MonitoredFilter.Monitored:
                query = query.Where(e => e.IsMonitored);
                break;
            case MonitoredFilter.Unmonitored:
                query = query.Where(e => !e.IsMonitored);
                break;
        }

        int totalCount = await query.CountAsync();

        bool ascending = sortDirection.HasValue
            ? sortDirection.Value == SortDirection.Asc
            : DefaultAscendingForScoreSortBy(sortBy);

        IOrderedQueryable<CustomFormatScoreEntry> ordered = sortBy switch
        {
            CfScoresSortBy.CurrentScore => ascending
                ? query.OrderBy(e => e.CurrentScore)
                : query.OrderByDescending(e => e.CurrentScore),
            CfScoresSortBy.CutoffScore => ascending
                ? query.OrderBy(e => e.CutoffScore)
                : query.OrderByDescending(e => e.CutoffScore),
            CfScoresSortBy.QualityProfile => ascending
                ? query.OrderBy(e => e.QualityProfileName)
                : query.OrderByDescending(e => e.QualityProfileName),
            CfScoresSortBy.LastSyncedAt => ascending
                ? query.OrderBy(e => e.LastSyncedAt)
                : query.OrderByDescending(e => e.LastSyncedAt),
            CfScoresSortBy.LastUpgradedAt => ascending
                ? query.OrderBy(e => e.LastUpgradedAt)
                : query.OrderByDescending(e => e.LastUpgradedAt),
            _ => ascending
                ? query.OrderBy(e => e.Title)
                : query.OrderByDescending(e => e.Title),
        };

        var items = await ordered
            .ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new CustomFormatScoreEntryResponse
            {
                Id = e.Id,
                ArrInstanceId = e.ArrInstanceId,
                ExternalItemId = e.ExternalItemId,
                EpisodeId = e.EpisodeId,
                ItemType = e.ItemType,
                Title = e.Title,
                FileId = e.FileId,
                CurrentScore = e.CurrentScore,
                CutoffScore = e.CutoffScore,
                QualityProfileName = e.QualityProfileName,
                IsBelowCutoff = e.CurrentScore < e.CutoffScore,
                IsMonitored = e.IsMonitored,
                LastSyncedAt = e.LastSyncedAt,
                LastUpgradedAt = e.LastUpgradedAt,
            })
            .ToListAsync();

        return Ok(new
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
        });
    }

    private static bool DefaultAscendingForScoreSortBy(CfScoresSortBy sortBy)
    {
        // Default directions match user expectations:
        // - textual fields sort ascending (A→Z)
        // - numeric/date fields sort descending (most recent / highest first)
        return sortBy switch
        {
            CfScoresSortBy.CurrentScore => false,
            CfScoresSortBy.CutoffScore => false,
            CfScoresSortBy.LastSyncedAt => false,
            CfScoresSortBy.LastUpgradedAt => false,
            _ => true,
        };
    }

    /// <summary>
    /// Gets recent CF score upgrades (where score improved in history).
    /// </summary>
    [HttpGet("upgrades")]
    public async Task<IActionResult> GetRecentUpgrades(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? instanceId = null,
        [FromQuery] int days = 30,
        [FromQuery] string? search = null,
        [FromQuery] CfUpgradesSortBy sortBy = CfUpgradesSortBy.UpgradedAt,
        [FromQuery] SortDirection? sortDirection = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 500) pageSize = 500;

        var query = _dataContext.CustomFormatScoreHistory
            .AsNoTracking()
            .AsQueryable();

        if (instanceId.HasValue)
        {
            query = query.Where(h => h.ArrInstanceId == instanceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(h => h.Title.ToLower().Contains(search.ToLower()));
        }

        if (days > 0)
        {
            query = query.Where(h => h.RecordedAt >= DateTime.UtcNow.AddDays(-days));
        }

        var allHistory = await query
            .OrderByDescending(h => h.RecordedAt)
            .ToListAsync();

        var upgrades = new List<CustomFormatScoreUpgradeResponse>();

        // Group by (ArrInstanceId, ExternalItemId, EpisodeId) and find score increases
        var grouped = allHistory
            .GroupBy(h => new { h.ArrInstanceId, h.ExternalItemId, h.EpisodeId });

        foreach (var group in grouped)
        {
            var entries = group.OrderBy(h => h.RecordedAt).ToList();
            for (int i = 1; i < entries.Count; i++)
            {
                if (entries[i].Score > entries[i - 1].Score)
                {
                    upgrades.Add(new CustomFormatScoreUpgradeResponse
                    {
                        ArrInstanceId = entries[i].ArrInstanceId,
                        ExternalItemId = entries[i].ExternalItemId,
                        EpisodeId = entries[i].EpisodeId,
                        ItemType = entries[i].ItemType,
                        Title = entries[i].Title,
                        PreviousScore = entries[i - 1].Score,
                        NewScore = entries[i].Score,
                        CutoffScore = entries[i].CutoffScore,
                        UpgradedAt = entries[i].RecordedAt,
                    });
                }
            }
        }

        bool ascending = sortDirection.HasValue
            ? sortDirection.Value == SortDirection.Asc
            : DefaultAscendingForUpgradeSortBy(sortBy);

        IEnumerable<CustomFormatScoreUpgradeResponse> sorted = sortBy switch
        {
            CfUpgradesSortBy.Title => ascending
                ? upgrades.OrderBy(u => u.Title, StringComparer.OrdinalIgnoreCase)
                : upgrades.OrderByDescending(u => u.Title, StringComparer.OrdinalIgnoreCase),
            CfUpgradesSortBy.NewScore => ascending
                ? upgrades.OrderBy(u => u.NewScore)
                : upgrades.OrderByDescending(u => u.NewScore),
            CfUpgradesSortBy.CutoffScore => ascending
                ? upgrades.OrderBy(u => u.CutoffScore)
                : upgrades.OrderByDescending(u => u.CutoffScore),
            CfUpgradesSortBy.PreviousScore => ascending
                ? upgrades.OrderBy(u => u.PreviousScore)
                : upgrades.OrderByDescending(u => u.PreviousScore),
            CfUpgradesSortBy.ScoreDelta => ascending
                ? upgrades.OrderBy(u => u.NewScore - u.PreviousScore)
                : upgrades.OrderByDescending(u => u.NewScore - u.PreviousScore),
            _ => ascending
                ? upgrades.OrderBy(u => u.UpgradedAt)
                : upgrades.OrderByDescending(u => u.UpgradedAt),
        };

        var sortedList = sorted.ToList();
        int totalCount = sortedList.Count;
        var paged = sortedList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new
        {
            Items = paged,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
        });
    }

    private static bool DefaultAscendingForUpgradeSortBy(CfUpgradesSortBy sortBy)
    {
        return sortBy switch
        {
            CfUpgradesSortBy.Title => true,
            _ => false,
        };
    }

    [HttpGet("instances")]
    public async Task<IActionResult> GetInstances()
    {
        var raw = await _dataContext.CustomFormatScoreEntries
            .AsNoTracking()
            .Join(
                _dataContext.ArrInstances.AsNoTracking(),
                e => e.ArrInstanceId,
                a => a.Id,
                (e, a) => new
                {
                    Id = e.ArrInstanceId,
                    a.Name,
                    e.ItemType,
                    e.QualityProfileName,
                })
            .ToListAsync();

        var instances = raw
            .GroupBy(x => new { x.Id, x.Name, x.ItemType })
            .Select(g => new
            {
                g.Key.Id,
                g.Key.Name,
                ItemType = g.Key.ItemType,
                QualityProfiles = g
                    .Select(x => x.QualityProfileName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            })
            .OrderBy(x => x.Name)
            .ToList();

        return Ok(new { Instances = instances });
    }

    /// <summary>
    /// Gets summary statistics for CF score tracking.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var entries = await _dataContext.CustomFormatScoreEntries
            .AsNoTracking()
            .ToListAsync();

        int totalTracked = entries.Count;
        int belowCutoff = entries.Count(e => e.CurrentScore < e.CutoffScore);
        int atOrAboveCutoff = totalTracked - belowCutoff;
        int monitored = entries.Count(e => e.IsMonitored);
        int unmonitored = totalTracked - monitored;

        // Count upgrades in the last 7 days
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var recentHistory = await _dataContext.CustomFormatScoreHistory
            .AsNoTracking()
            .Where(h => h.RecordedAt >= sevenDaysAgo)
            .OrderBy(h => h.RecordedAt)
            .ToListAsync();

        int recentUpgrades = 0;
        var recentGrouped = recentHistory
            .GroupBy(h => new { h.ArrInstanceId, h.ExternalItemId, h.EpisodeId });

        foreach (var group in recentGrouped)
        {
            var ordered = group.OrderBy(h => h.RecordedAt).ToList();
            for (int i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Score > ordered[i - 1].Score)
                    recentUpgrades++;
            }
        }

        // Per-instance stats
        var instanceIds = entries.Select(e => e.ArrInstanceId).Distinct().ToList();
        var instances = await _dataContext.ArrInstances
            .AsNoTracking()
            .Include(a => a.ArrConfig)
            .Where(a => instanceIds.Contains(a.Id))
            .ToListAsync();

        var perInstanceStats = instanceIds.Select(instanceId =>
        {
            var instanceEntries = entries.Where(e => e.ArrInstanceId == instanceId).ToList();
            int instTracked = instanceEntries.Count;
            int instBelow = instanceEntries.Count(e => e.CurrentScore < e.CutoffScore);
            int instMonitored = instanceEntries.Count(e => e.IsMonitored);

            int instUpgrades = 0;
            var instHistory = recentGrouped
                .Where(g => g.Key.ArrInstanceId == instanceId);
            foreach (var group in instHistory)
            {
                var ordered = group.OrderBy(h => h.RecordedAt).ToList();
                for (int i = 1; i < ordered.Count; i++)
                {
                    if (ordered[i].Score > ordered[i - 1].Score)
                        instUpgrades++;
                }
            }

            var instance = instances.FirstOrDefault(a => a.Id == instanceId);
            return new InstanceCfScoreStat
            {
                InstanceId = instanceId,
                InstanceName = instance?.Name ?? "Unknown",
                InstanceType = instance?.ArrConfig.Type.ToString() ?? "Unknown",
                TotalTracked = instTracked,
                BelowCutoff = instBelow,
                AtOrAboveCutoff = instTracked - instBelow,
                Monitored = instMonitored,
                Unmonitored = instTracked - instMonitored,
                RecentUpgrades = instUpgrades,
            };
        }).OrderBy(s => s.InstanceName).ToList();

        return Ok(new CustomFormatScoreStatsResponse
        {
            TotalTracked = totalTracked,
            BelowCutoff = belowCutoff,
            AtOrAboveCutoff = atOrAboveCutoff,
            Monitored = monitored,
            Unmonitored = unmonitored,
            RecentUpgrades = recentUpgrades,
            PerInstanceStats = perInstanceStats,
        });
    }

    /// <summary>
    /// Gets CF score history for a specific item.
    /// </summary>
    [HttpGet("{instanceId}/{itemId}/history")]
    public async Task<IActionResult> GetItemHistory(
        Guid instanceId,
        long itemId,
        [FromQuery] long episodeId = 0)
    {
        var history = await _dataContext.CustomFormatScoreHistory
            .AsNoTracking()
            .Where(h => h.ArrInstanceId == instanceId
                        && h.ExternalItemId == itemId
                        && h.EpisodeId == episodeId)
            .OrderByDescending(h => h.RecordedAt)
            .Select(h => new CustomFormatScoreHistoryEntryResponse
            {
                Score = h.Score,
                CutoffScore = h.CutoffScore,
                RecordedAt = h.RecordedAt,
            })
            .ToListAsync();

        return Ok(new { Entries = history });
    }
}

