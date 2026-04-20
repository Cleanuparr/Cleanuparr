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
        [FromQuery] string sortBy = "title",
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? qualityProfile = null,
        [FromQuery] string? itemType = null,
        [FromQuery] string? cutoffFilter = null,
        [FromQuery] string? monitoredFilter = null,
        [FromQuery] bool? hideMet = null,
        [FromQuery] bool? hideUnmonitored = null)
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

        // Back-compat: translate deprecated inverted booleans into the new positive-logic params
        // when the caller did not provide an explicit new param.
        if (string.IsNullOrWhiteSpace(cutoffFilter) && hideMet == true)
        {
            cutoffFilter = "below";
        }
        if (string.IsNullOrWhiteSpace(monitoredFilter) && hideUnmonitored == true)
        {
            monitoredFilter = "monitored";
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

        if (!string.IsNullOrWhiteSpace(itemType)
            && Enum.TryParse<InstanceType>(itemType, ignoreCase: true, out var parsedType))
        {
            query = query.Where(e => e.ItemType == parsedType);
        }

        switch ((cutoffFilter ?? "all").ToLowerInvariant())
        {
            case "below":
                query = query.Where(e => e.CurrentScore < e.CutoffScore);
                break;
            case "met":
                query = query.Where(e => e.CurrentScore >= e.CutoffScore);
                break;
        }

        switch ((monitoredFilter ?? "all").ToLowerInvariant())
        {
            case "monitored":
                query = query.Where(e => e.IsMonitored);
                break;
            case "unmonitored":
                query = query.Where(e => !e.IsMonitored);
                break;
        }

        int totalCount = await query.CountAsync();

        bool ascending = string.IsNullOrWhiteSpace(sortDirection)
            ? DefaultAscendingForScoreSortBy(sortBy)
            : string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<CustomFormatScoreEntry> ordered = (sortBy ?? "title").ToLowerInvariant() switch
        {
            "currentscore" => ascending
                ? query.OrderBy(e => e.CurrentScore)
                : query.OrderByDescending(e => e.CurrentScore),
            "cutoffscore" => ascending
                ? query.OrderBy(e => e.CutoffScore)
                : query.OrderByDescending(e => e.CutoffScore),
            "qualityprofile" => ascending
                ? query.OrderBy(e => e.QualityProfileName)
                : query.OrderByDescending(e => e.QualityProfileName),
            "lastsyncedat" or "date" => ascending
                ? query.OrderBy(e => e.LastSyncedAt)
                : query.OrderByDescending(e => e.LastSyncedAt),
            "lastupgradedat" => ascending
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

    private static bool DefaultAscendingForScoreSortBy(string? sortBy)
    {
        // Default directions match user expectations:
        // - textual fields sort ascending (A→Z)
        // - numeric/date fields sort descending (most recent / highest first)
        return (sortBy ?? "title").ToLowerInvariant() switch
        {
            "currentscore" or "cutoffscore" or "lastsyncedat" or "date" or "lastupgradedat" => false,
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
        [FromQuery] int days = 30)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 500) pageSize = 500;

        // Find history entries where a newer entry has a higher score than an older one
        // We group by item and look for score increases between consecutive records
        var query = _dataContext.CustomFormatScoreHistory
            .AsNoTracking()
            .AsQueryable();

        if (instanceId.HasValue)
        {
            query = query.Where(h => h.ArrInstanceId == instanceId.Value);
        }

        var allHistory = await query
            .Where(h => h.RecordedAt >= DateTime.UtcNow.AddDays(-days))
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

        // Sort by most recent upgrade first
        upgrades = upgrades.OrderByDescending(u => u.UpgradedAt).ToList();

        int totalCount = upgrades.Count;
        var paged = upgrades
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

    [HttpGet("instances")]
    public async Task<IActionResult> GetInstances()
    {
        var instances = await _dataContext.CustomFormatScoreEntries
            .AsNoTracking()
            .Select(e => new { e.ArrInstanceId, e.ItemType })
            .Distinct()
            .Join(
                _dataContext.ArrInstances.AsNoTracking(),
                e => e.ArrInstanceId,
                a => a.Id,
                (e, a) => new
                {
                    Id = e.ArrInstanceId,
                    a.Name,
                    e.ItemType,
                })
            .OrderBy(x => x.Name)
            .ToListAsync();

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

