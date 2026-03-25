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
        List<Guid> currentCycleIds = instanceConfigs.Select(ic => ic.CurrentCycleId).ToList();
        var cycleItemsByInstance = await _dataContext.SeekerHistory
            .AsNoTracking()
            .Where(h => currentCycleIds.Contains(h.CycleId))
            .GroupBy(h => h.ArrInstanceId)
            .Select(g => new
            {
                InstanceId = g.Key,
                CycleItemsSearched = g.Select(h => h.ExternalItemId).Distinct().Count(),
                CycleStartedAt = (DateTime?)g.Min(h => h.LastSearchedAt),
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
                CurrentCycleId = ic.CurrentCycleId,
                CycleItemsSearched = cycleProgress?.CycleItemsSearched ?? 0,
                CycleItemsTotal = ic.TotalEligibleItems,
                CycleStartedAt = cycleProgress?.CycleStartedAt,
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
    /// Gets paginated search-triggered events with decoded data.
    /// Supports optional text search across item names in event data.
    /// </summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? instanceId = null,
        [FromQuery] Guid? cycleId = null,
        [FromQuery] string? search = null)
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

        // Filter by cycle ID
        if (cycleId.HasValue)
        {
            query = query.Where(e => e.CycleId == cycleId.Value);
        }

        // Pre-filter by search term on the JSON data field
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e => e.Data != null && e.Data.ToLower().Contains(search.ToLower()));
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
                CycleId = e.CycleId,
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
        catch (JsonException)
        {
            return ("Unknown", 0, [], SeekerSearchType.Proactive, null);
        }
    }
}
