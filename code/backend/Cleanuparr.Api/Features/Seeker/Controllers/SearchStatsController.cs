using System.Text.Json;
using Cleanuparr.Api.Features.Seeker.Contracts.Responses;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.Seeker.Controllers;

[ApiController]
[Route("api/seeker/search-stats")]
[Authorize]
public sealed class SearchStatsController : ControllerBase
{
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;

    public SearchStatsController(DataContext dataContext, EventsContext eventsContext)
    {
        _dataContext = dataContext;
        _eventsContext = eventsContext;
    }

    /// <summary>
    /// Gets aggregate search statistics across all instances.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        DateTime sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        DateTime thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        // Event counts from EventsContext
        var searchEvents = _eventsContext.Events
            .AsNoTracking()
            .Where(e => e.EventType == EventType.SearchTriggered);

        int totalSearchesAllTime = await searchEvents.CountAsync();
        int searchesLast7Days = await searchEvents.CountAsync(e => e.Timestamp >= sevenDaysAgo);
        int searchesLast30Days = await searchEvents.CountAsync(e => e.Timestamp >= thirtyDaysAgo);

        // History stats from DataContext
        int uniqueItemsSearched = await _dataContext.SeekerHistory
            .AsNoTracking()
            .Select(h => h.ExternalItemId)
            .Distinct()
            .CountAsync();

        int pendingReplacementSearches = await _dataContext.SearchQueue.CountAsync();

        // Per-instance stats
        List<SeekerInstanceConfig> instanceConfigs = await _dataContext.SeekerInstanceConfigs
            .AsNoTracking()
            .Include(s => s.ArrInstance)
                .ThenInclude(a => a.ArrConfig)
            .Where(s => s.Enabled && s.ArrInstance.Enabled)
            .ToListAsync();

        var historyByInstance = await _dataContext.SeekerHistory
            .AsNoTracking()
            .GroupBy(h => h.ArrInstanceId)
            .Select(g => new
            {
                InstanceId = g.Key,
                ItemsTracked = g.Select(h => h.ExternalItemId).Distinct().Count(),
                LastSearchedAt = g.Max(h => h.LastSearchedAt),
                TotalSearchCount = g.Sum(h => h.SearchCount),
            })
            .ToListAsync();

        // Count items searched in current cycle per instance
        List<Guid> currentRunIds = instanceConfigs.Select(ic => ic.CurrentRunId).ToList();
        var cycleItemsByInstance = await _dataContext.SeekerHistory
            .AsNoTracking()
            .Where(h => currentRunIds.Contains(h.RunId))
            .GroupBy(h => h.ArrInstanceId)
            .Select(g => new
            {
                InstanceId = g.Key,
                CycleItemsSearched = g.Select(h => h.ExternalItemId).Distinct().Count(),
            })
            .ToListAsync();

        var perInstanceStats = instanceConfigs.Select(ic =>
        {
            var history = historyByInstance.FirstOrDefault(h => h.InstanceId == ic.ArrInstanceId);
            var cycleProgress = cycleItemsByInstance.FirstOrDefault(c => c.InstanceId == ic.ArrInstanceId);
            return new InstanceSearchStat
            {
                InstanceId = ic.ArrInstanceId,
                InstanceName = ic.ArrInstance.Name,
                InstanceType = ic.ArrInstance.ArrConfig.Type.ToString(),
                ItemsTracked = history?.ItemsTracked ?? 0,
                TotalSearchCount = history?.TotalSearchCount ?? 0,
                LastSearchedAt = history?.LastSearchedAt,
                LastProcessedAt = ic.LastProcessedAt,
                CurrentRunId = ic.CurrentRunId,
                CycleItemsSearched = cycleProgress?.CycleItemsSearched ?? 0,
                CycleItemsTotal = ic.TotalEligibleItems,
            };
        }).ToList();

        return Ok(new SearchStatsSummaryResponse
        {
            TotalSearchesAllTime = totalSearchesAllTime,
            SearchesLast7Days = searchesLast7Days,
            SearchesLast30Days = searchesLast30Days,
            UniqueItemsSearched = uniqueItemsSearched,
            PendingReplacementSearches = pendingReplacementSearches,
            EnabledInstances = instanceConfigs.Count,
            PerInstanceStats = perInstanceStats,
        });
    }

    /// <summary>
    /// Gets paginated search history from SeekerHistory.
    /// Supports sorting by lastSearched (default) or searchCount.
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? instanceId = null,
        [FromQuery] string sortBy = "lastSearched")
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 100) pageSize = 100;

        var query = _dataContext.SeekerHistory
            .AsNoTracking()
            .Include(h => h.ArrInstance)
                .ThenInclude(a => a.ArrConfig)
            .AsQueryable();

        if (instanceId.HasValue)
        {
            query = query.Where(h => h.ArrInstanceId == instanceId.Value);
        }

        // Group by item across cycles to aggregate search counts
        var grouped = query
            .GroupBy(h => new { h.ArrInstanceId, h.ExternalItemId, h.ItemType, h.SeasonNumber })
            .Select(g => new
            {
                g.Key.ArrInstanceId,
                g.Key.ExternalItemId,
                g.Key.SeasonNumber,
                TotalSearchCount = g.Sum(h => h.SearchCount),
                LastSearchedAt = g.Max(h => h.LastSearchedAt),
                // Pick the most recent row's data for display fields
                ItemTitle = g.OrderByDescending(h => h.LastSearchedAt).First().ItemTitle,
                SearchCount = g.OrderByDescending(h => h.LastSearchedAt).First().SearchCount,
                InstanceName = g.OrderByDescending(h => h.LastSearchedAt).First().ArrInstance.Name,
                InstanceType = g.OrderByDescending(h => h.LastSearchedAt).First().ArrInstance.ArrConfig.Type,
                Id = g.OrderByDescending(h => h.LastSearchedAt).First().Id,
            });

        int totalCount = await grouped.CountAsync();

        var ordered = sortBy switch
        {
            "searchCount" => grouped.OrderByDescending(g => g.TotalSearchCount),
            _ => grouped.OrderByDescending(g => g.LastSearchedAt),
        };

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new SearchHistoryEntryResponse
            {
                Id = g.Id,
                ArrInstanceId = g.ArrInstanceId,
                InstanceName = g.InstanceName,
                InstanceType = g.InstanceType.ToString(),
                ExternalItemId = g.ExternalItemId,
                ItemTitle = g.ItemTitle,
                SeasonNumber = g.SeasonNumber,
                LastSearchedAt = g.LastSearchedAt,
                SearchCount = g.SearchCount,
                TotalSearchCount = g.TotalSearchCount,
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
    /// Gets paginated search-triggered events with decoded data.
    /// </summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? instanceId = null,
        [FromQuery] Guid? cycleRunId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 100) pageSize = 100;

        var query = _eventsContext.Events
            .AsNoTracking()
            .Where(e => e.EventType == EventType.SearchTriggered);

        // Filter by instance URL if instanceId provided
        if (instanceId.HasValue)
        {
            var instance = await _dataContext.ArrInstances
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == instanceId.Value);

            if (instance is not null)
            {
                string url = (instance.ExternalUrl ?? instance.Url).ToString();
                query = query.Where(e => e.InstanceUrl == url);
            }
        }

        // Filter by cycle run ID
        if (cycleRunId.HasValue)
        {
            query = query.Where(e => e.CycleRunId == cycleRunId.Value);
        }

        int totalCount = await query.CountAsync();

        var rawEvents = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = rawEvents.Select(e =>
        {
            var parsed = ParseEventData(e.Data);
            return new SearchEventResponse
            {
                Id = e.Id,
                Timestamp = e.Timestamp,
                InstanceName = parsed.InstanceName,
                InstanceType = e.InstanceType?.ToString(),
                ItemCount = parsed.ItemCount,
                Items = parsed.Items,
                SearchType = parsed.SearchType,
                SearchStatus = e.SearchStatus,
                CompletedAt = e.CompletedAt,
                GrabbedItems = parsed.GrabbedItems,
                CycleRunId = e.CycleRunId,
                IsDryRun = e.IsDryRun,
            };
        }).ToList();

        return Ok(new
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
        });
    }

    private static (string InstanceName, int ItemCount, List<string> Items, SeekerSearchType SearchType, object? GrabbedItems) ParseEventData(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return ("Unknown", 0, [], SeekerSearchType.Proactive, null);
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            string instanceName = root.TryGetProperty("InstanceName", out var nameEl)
                ? nameEl.GetString() ?? "Unknown"
                : "Unknown";

            int itemCount = root.TryGetProperty("ItemCount", out var countEl)
                ? countEl.GetInt32()
                : 0;

            var items = new List<string>();
            if (root.TryGetProperty("Items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in itemsEl.EnumerateArray())
                {
                    string? val = item.GetString();
                    if (val is not null) items.Add(val);
                }
            }

            SeekerSearchType searchType = root.TryGetProperty("SearchType", out var typeEl)
                && Enum.TryParse<SeekerSearchType>(typeEl.GetString(), out var parsed)
                ? parsed
                : SeekerSearchType.Proactive;

            object? grabbedItems = root.TryGetProperty("GrabbedItems", out var grabbedEl)
                ? JsonSerializer.Deserialize<object>(grabbedEl.GetRawText())
                : null;

            return (instanceName, itemCount, items, searchType, grabbedItems);
        }
        catch
        {
            return ("Unknown", 0, [], SeekerSearchType.Proactive, null);
        }
    }
}
