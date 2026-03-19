using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.Seeker.Controllers;

[ApiController]
[Route("api/seeker/cf-scores")]
[Authorize]
public sealed class CfScoreController : ControllerBase
{
    private readonly DataContext _dataContext;

    public CfScoreController(DataContext dataContext)
    {
        _dataContext = dataContext;
    }

    /// <summary>
    /// Gets current CF scores with pagination, optionally filtered by instance.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCfScores(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? instanceId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 100) pageSize = 100;

        var query = _dataContext.CfScoreEntries
            .AsNoTracking()
            .AsQueryable();

        if (instanceId.HasValue)
        {
            query = query.Where(e => e.ArrInstanceId == instanceId.Value);
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(e => e.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new CfScoreEntryResponse
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
                LastSyncedAt = e.LastSyncedAt,
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

    /// <summary>
    /// Gets recent CF score upgrades (where score improved in history).
    /// </summary>
    [HttpGet("upgrades")]
    public async Task<IActionResult> GetRecentUpgrades(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? instanceId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // Find history entries where a newer entry has a higher score than an older one
        // We group by item and look for score increases between consecutive records
        var query = _dataContext.CfScoreHistory
            .AsNoTracking()
            .AsQueryable();

        if (instanceId.HasValue)
        {
            query = query.Where(h => h.ArrInstanceId == instanceId.Value);
        }

        // Get all history ordered by item + time, then detect upgrades in memory
        // This is acceptable because history entries are deduplicated (only written on change)
        var allHistory = await query
            .OrderByDescending(h => h.RecordedAt)
            .ToListAsync();

        var upgrades = new List<CfScoreUpgradeResponse>();

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
                    upgrades.Add(new CfScoreUpgradeResponse
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

    /// <summary>
    /// Gets summary statistics for CF score tracking.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var entries = await _dataContext.CfScoreEntries
            .AsNoTracking()
            .ToListAsync();

        int totalTracked = entries.Count;
        int belowCutoff = entries.Count(e => e.CurrentScore < e.CutoffScore);
        int atOrAboveCutoff = totalTracked - belowCutoff;

        // Count upgrades in the last 7 days
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var recentHistory = await _dataContext.CfScoreHistory
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

        double avgScore = entries.Count > 0 ? entries.Average(e => e.CurrentScore) : 0;

        return Ok(new CfScoreStatsResponse
        {
            TotalTracked = totalTracked,
            BelowCutoff = belowCutoff,
            AtOrAboveCutoff = atOrAboveCutoff,
            RecentUpgrades = recentUpgrades,
            AverageScore = Math.Round(avgScore, 1),
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
        var history = await _dataContext.CfScoreHistory
            .AsNoTracking()
            .Where(h => h.ArrInstanceId == instanceId
                        && h.ExternalItemId == itemId
                        && h.EpisodeId == episodeId)
            .OrderByDescending(h => h.RecordedAt)
            .Select(h => new CfScoreHistoryEntryResponse
            {
                Score = h.Score,
                CutoffScore = h.CutoffScore,
                RecordedAt = h.RecordedAt,
            })
            .ToListAsync();

        return Ok(new { Entries = history });
    }
}

public sealed record CfScoreEntryResponse
{
    public Guid Id { get; init; }
    public Guid ArrInstanceId { get; init; }
    public long ExternalItemId { get; init; }
    public long EpisodeId { get; init; }
    public InstanceType ItemType { get; init; }
    public string Title { get; init; } = string.Empty;
    public long FileId { get; init; }
    public int CurrentScore { get; init; }
    public int CutoffScore { get; init; }
    public string QualityProfileName { get; init; } = string.Empty;
    public bool IsBelowCutoff { get; init; }
    public DateTime LastSyncedAt { get; init; }
}

public sealed record CfScoreUpgradeResponse
{
    public Guid ArrInstanceId { get; init; }
    public long ExternalItemId { get; init; }
    public long EpisodeId { get; init; }
    public InstanceType ItemType { get; init; }
    public string Title { get; init; } = string.Empty;
    public int PreviousScore { get; init; }
    public int NewScore { get; init; }
    public int CutoffScore { get; init; }
    public DateTime UpgradedAt { get; init; }
}

public sealed record CfScoreStatsResponse
{
    public int TotalTracked { get; init; }
    public int BelowCutoff { get; init; }
    public int AtOrAboveCutoff { get; init; }
    public int RecentUpgrades { get; init; }
    public double AverageScore { get; init; }
}

public sealed record CfScoreHistoryEntryResponse
{
    public int Score { get; init; }
    public int CutoffScore { get; init; }
    public DateTime RecordedAt { get; init; }
}
