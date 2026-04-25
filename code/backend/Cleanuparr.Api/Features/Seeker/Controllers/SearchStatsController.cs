using Cleanuparr.Api.Features.Seeker.Contracts.Responses;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using AppEvent = Cleanuparr.Persistence.Models.Events.AppEvent;
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
    /// Gets paginated search-triggered events with optional filtering and sorting.
    /// Results default to newest-first by timestamp. Ties on non-timestamp sort keys
    /// fall back to <c>Timestamp</c> descending for stable ordering.
    /// </summary>
    /// <param name="page">1-based page number. Clamped to at least 1.</param>
    /// <param name="pageSize">Rows per page. Clamped to the inclusive range [1, 100]; defaults to 50.</param>
    /// <param name="instanceId">When set, restricts results to events produced by this *arr instance.</param>
    /// <param name="cycleId">When set, restricts results to events from this seeker cycle.</param>
    /// <param name="search">Case-insensitive substring match against the stored item title.</param>
    /// <param name="sortBy">Primary sort column. Defaults to <see cref="SearchEventsSortBy.Timestamp"/>.</param>
    /// <param name="sortDirection">Sort direction for the primary column. Defaults to descending.</param>
    /// <param name="searchStatus">When supplied, keeps only events whose <see cref="SearchCommandStatus"/> appears in this list.</param>
    /// <param name="searchType">When supplied, keeps only events matching this <see cref="SeekerSearchType"/>.</param>
    /// <param name="searchReason">When supplied, keeps only events matching this <see cref="SeekerSearchReason"/>.</param>
    /// <param name="grabbed">When <c>true</c>, keeps only events that recorded at least one grabbed item; when <c>false</c>, keeps only events with none.</param>
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? instanceId = null,
        [FromQuery] Guid? cycleId = null,
        [FromQuery] string? search = null,
        [FromQuery] SearchEventsSortBy sortBy = SearchEventsSortBy.Timestamp,
        [FromQuery] SortDirection sortDirection = SortDirection.Desc,
        [FromQuery] SearchCommandStatus[]? searchStatus = null,
        [FromQuery] SeekerSearchType? searchType = null,
        [FromQuery] SeekerSearchReason? searchReason = null,
        [FromQuery] bool? grabbed = null)
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

        var query = _eventsContext.Events
            .AsNoTracking()
            .Include(e => e.SearchEventData)
            .Where(e => e.EventType == EventType.SearchTriggered);

        // Filter by instance ID
        if (instanceId.HasValue)
        {
            query = query.Where(e => e.ArrInstanceId == instanceId.Value);
        }

        // Filter by cycle ID
        if (cycleId.HasValue)
        {
            query = query.Where(e => e.CycleId == cycleId.Value);
        }

        // Search by item title in SearchEventData
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = EventsContext.GetLikePattern(search);
            query = query.Where(e => e.SearchEventData != null
                && EF.Functions.Like(e.SearchEventData.ItemTitle, pattern));
        }

        // Filter by search status (multi-valued)
        if (searchStatus is { Length: > 0 })
        {
            SearchCommandStatus[] statuses = searchStatus.Distinct().ToArray();
            query = query.Where(e => e.SearchStatus.HasValue && statuses.Contains(e.SearchStatus.Value));
        }

        if (searchType.HasValue)
        {
            SeekerSearchType typeValue = searchType.Value;
            query = query.Where(e => e.SearchEventData != null && e.SearchEventData.SearchType == typeValue);
        }

        if (searchReason.HasValue)
        {
            SeekerSearchReason reasonValue = searchReason.Value;
            query = query.Where(e => e.SearchEventData != null && e.SearchEventData.SearchReason == reasonValue);
        }

        // Filter by grabbed-result presence
        if (grabbed.HasValue)
        {
            if (grabbed.Value)
            {
                query = query.Where(e => e.SearchEventData != null && e.SearchEventData.GrabbedItems.Count > 0);
            }
            else
            {
                query = query.Where(e => e.SearchEventData == null || e.SearchEventData.GrabbedItems.Count == 0);
            }
        }

        int totalCount = await query.CountAsync();

        bool ascending = sortDirection == SortDirection.Asc;

        IOrderedQueryable<AppEvent> ordered = sortBy switch
        {
            SearchEventsSortBy.Title => ascending
                ? query.OrderBy(e => e.SearchEventData != null ? e.SearchEventData.ItemTitle : string.Empty)
                : query.OrderByDescending(e => e.SearchEventData != null ? e.SearchEventData.ItemTitle : string.Empty),
            SearchEventsSortBy.Status => ascending
                ? query.OrderBy(e => e.SearchStatus)
                : query.OrderByDescending(e => e.SearchStatus),
            SearchEventsSortBy.Type => ascending
                ? query.OrderBy(e => e.SearchEventData != null ? (int)e.SearchEventData.SearchType : 0)
                : query.OrderByDescending(e => e.SearchEventData != null ? (int)e.SearchEventData.SearchType : 0),
            _ => ascending
                ? query.OrderBy(e => e.Timestamp)
                : query.OrderByDescending(e => e.Timestamp),
        };

        // Secondary sort by timestamp desc for stable ordering when primary ties
        if (sortBy != SearchEventsSortBy.Timestamp)
        {
            ordered = ordered.ThenByDescending(e => e.Timestamp);
        }

        var rawEvents = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Resolve instance types from DataContext via ArrInstanceId
        var arrInstanceIds = rawEvents
            .Where(e => e.ArrInstanceId.HasValue)
            .Select(e => e.ArrInstanceId!.Value)
            .Distinct()
            .ToList();

        var instanceTypeMap = arrInstanceIds.Count > 0
            ? await _dataContext.ArrInstances
                .AsNoTracking()
                .Include(a => a.ArrConfig)
                .Where(a => arrInstanceIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.ArrConfig.Type)
            : new Dictionary<Guid, InstanceType>();

        var items = rawEvents.Select(e => new SearchEventResponse
        {
            Id = e.Id,
            Timestamp = e.Timestamp,
            ArrInstanceId = e.ArrInstanceId,
            InstanceType = e.ArrInstanceId.HasValue && instanceTypeMap.TryGetValue(e.ArrInstanceId.Value, out var it)
                ? it.ToString()
                : null,
            ItemTitle = e.SearchEventData?.ItemTitle ?? "Unknown",
            SearchType = e.SearchEventData?.SearchType ?? SeekerSearchType.Proactive,
            SearchReason = e.SearchEventData?.SearchReason,
            SearchStatus = e.SearchStatus,
            CompletedAt = e.CompletedAt,
            GrabbedItems = e.SearchEventData?.GrabbedItems ?? [],
            CycleId = e.CycleId,
            IsDryRun = e.IsDryRun,
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
}
