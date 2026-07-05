using System.Linq.Expressions;
using System.Text.Json.Serialization;
using Cleanuparr.Api.Features.Events.Contracts.Responses;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly EventsContext _context;

    public EventsController(EventsContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets events with pagination and filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<EventListItem>>> GetEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? severity = null,
        [FromQuery] string? eventType = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        [FromQuery] string? search = null,
        [FromQuery] string? jobRunId = null)
    {
        // Validate pagination parameters
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
        
        IQueryable<EventListItem> query = _context.Events
            .Select(EventListItem.FromEvent)
            .Concat(_context.EventHistory.Select(EventListItem.FromHistory));

        // Apply filters
        if (!string.IsNullOrWhiteSpace(severity))
        {
            if (Enum.TryParse<EventSeverity>(severity, true, out EventSeverity severityEnum))
            {
                query = query.Where(e => e.Severity == severityEnum);
            }
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            if (Enum.TryParse<EventType>(eventType, true, out EventType eventTypeEnum))
            {
                query = query.Where(e => e.EventType == eventTypeEnum);
            }
        }

        // Apply date range filters
        if (fromDate.HasValue)
        {
            query = query.Where(e => e.Timestamp >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(e => e.Timestamp <= toDate.Value);
        }

        // Apply job run ID exact-match filter
        if (!string.IsNullOrWhiteSpace(jobRunId) && Guid.TryParse(jobRunId, out Guid jobRunGuid))
        {
            query = query.Where(e => e.JobRunId == jobRunGuid);
        }

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = EventsContext.GetLikePattern(search);
            query = query.Where(e =>
                EF.Functions.Like(e.Message, pattern) ||
                (e.ItemTitle != null && EF.Functions.Like(e.ItemTitle, pattern)) ||
                EF.Functions.Like(e.TrackingId.ToString(), pattern) ||
                EF.Functions.Like(e.JobRunId.ToString(), pattern)
            );
        }

        // Count total matching records for pagination
        int totalCount = await query.CountAsync();

        // Calculate pagination
        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        int skip = (page - 1) * pageSize;

        List<EventListItem> events = await query
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        // Return paginated result
        PaginatedResult<EventListItem> result = new()
        {
            Items = events,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        return Ok(result);
    }

    /// <summary>
    /// Gets a specific event by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AppEvent>> GetEvent(Guid id)
    {
        var eventEntity = await _context.Events.FindAsync(id);
        
        if (eventEntity == null)
            return NotFound();

        return Ok(eventEntity);
    }

    /// <summary>
    /// Gets events by tracking ID
    /// </summary>
    [HttpGet("tracking/{trackingId}")]
    public async Task<ActionResult<List<AppEvent>>> GetEventsByTracking(Guid trackingId)
    {
        var events = await _context.Events
            .Where(e => e.TrackingId == trackingId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>
    /// Gets unique event types
    /// </summary>
    [HttpGet("types")]
    public async Task<ActionResult<List<string>>> GetEventTypes()
    {
        var types = Enum.GetNames(typeof(EventType)).ToList();
        return Ok(types);
    }

    /// <summary>
    /// Gets unique severities
    /// </summary>
    [HttpGet("severities")]
    public async Task<ActionResult<List<string>>> GetSeverities()
    {
        var severities = Enum.GetNames(typeof(EventSeverity)).ToList();
        return Ok(severities);
    }

    [HttpGet("timeline")]
    public async Task<ActionResult<EventTypeTimelineResponse>> GetTimeline([FromQuery] int hours = 720)
    {
        hours = Math.Clamp(hours, 1, 8760);
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddHours(-hours);

        var rows = await _context.Events
            .Where(e => e.Timestamp >= cutoff)
            .Select(e => new { e.Timestamp, e.EventType })
            .Concat(_context.EventHistory
                .Where(e => e.Timestamp >= cutoff)
                .Select(e => new { e.Timestamp, e.EventType }))
            .ToListAsync();

        Dictionary<(DateOnly Day, EventType Type), int> byDayType = rows
            .GroupBy(r => (Day: DateOnly.FromDateTime(r.Timestamp.UtcDateTime), r.EventType))
            .ToDictionary(g => g.Key, g => g.Count());

        List<EventType> presentTypes = rows
            .Select(r => r.EventType)
            .Distinct()
            .OrderBy(t => (int)t)
            .ToList();

        List<EventTypeTimelineBucket> buckets = [];
        DateOnly start = DateOnly.FromDateTime(cutoff.UtcDateTime);
        DateOnly end = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        for (DateOnly day = start; day <= end; day = day.AddDays(1))
        {
            Dictionary<string, int> counts = new();
            foreach (EventType type in presentTypes)
            {
                if (byDayType.TryGetValue((day, type), out int count) && count > 0)
                {
                    counts[type.ToString()] = count;
                }
            }

            buckets.Add(new EventTypeTimelineBucket { Date = day, Counts = counts });
        }

        return Ok(new EventTypeTimelineResponse
        {
            Types = presentTypes.Select(t => t.ToString()).ToList(),
            Buckets = buckets,
        });
    }
} 

/// <summary>
/// Represents a paginated result set
/// </summary>
/// <typeparam name="T">Type of items in the result</typeparam>
public class PaginatedResult<T>
{
    /// <summary>
    /// The items in the current page
    /// </summary>
    public List<T> Items { get; set; } = new();
    
    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int Page { get; set; }
    
    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; }
    
    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalCount { get; set; }
    
    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages { get; set; }
    
    /// <summary>
    /// Whether there is a previous page
    /// </summary>
    [JsonIgnore]
    public bool HasPrevious => Page > 1;
    
    /// <summary>
    /// Whether there is a next page
    /// </summary>
    [JsonIgnore]
    public bool HasNext => Page < TotalPages;
}

/// <summary>
/// Flattened event row for the events list. Unifies active <see cref="AppEvent"/> rows and
/// archived <see cref="EventHistory"/> rows so both are browsable through one paginated endpoint.
/// Serializes identically to the fields the client reads from <see cref="AppEvent"/>.
/// </summary>
public class EventListItem
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public EventType EventType { get; set; }
    public string Message { get; set; } = string.Empty;
    public EventSeverity Severity { get; set; }
    public Guid? TrackingId { get; set; }
    public Guid? StrikeId { get; set; }
    public Guid? JobRunId { get; set; }
    public Guid? ArrInstanceId { get; set; }
    public Guid? DownloadClientId { get; set; }
    public SearchCommandStatus? SearchStatus { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? CycleId { get; set; }
    public bool IsDryRun { get; set; }
    public string? ItemTitle { get; set; }
    public string? ItemHash { get; set; }
    public int? StrikeCount { get; set; }
    public List<string> FailedImportReasons { get; set; } = [];
    public DeleteReason? DeleteReason { get; set; }
    public bool? RemoveFromClient { get; set; }
    public CleanReason? CleanReason { get; set; }
    public string? CleanedCategory { get; set; }
    public double? SeedRatio { get; set; }
    public double? SeedingTimeHours { get; set; }
    public string? OldCategory { get; set; }
    public string? NewCategory { get; set; }
    public bool? IsCategoryTag { get; set; }
    public SeekerSearchType? SearchType { get; set; }
    public SeekerSearchReason? SearchReason { get; set; }
    public List<string> GrabbedItems { get; set; } = [];

    /// <summary>Projects an active <see cref="AppEvent"/> into the unified list shape.</summary>
    public static readonly Expression<Func<AppEvent, EventListItem>> FromEvent = e => new EventListItem
    {
        Id = e.Id,
        Timestamp = e.Timestamp,
        EventType = e.EventType,
        Message = e.Message,
        Severity = e.Severity,
        TrackingId = e.TrackingId,
        StrikeId = e.StrikeId,
        JobRunId = e.JobRunId,
        ArrInstanceId = e.ArrInstanceId,
        DownloadClientId = e.DownloadClientId,
        SearchStatus = e.SearchStatus,
        CompletedAt = e.CompletedAt,
        CycleId = e.CycleId,
        IsDryRun = e.IsDryRun,
        ItemTitle = e.ItemTitle,
        ItemHash = e.ItemHash,
        StrikeCount = e.StrikeCount,
        FailedImportReasons = e.FailedImportReasons,
        DeleteReason = e.DeleteReason,
        RemoveFromClient = e.RemoveFromClient,
        CleanReason = e.CleanReason,
        CleanedCategory = e.CleanedCategory,
        SeedRatio = e.SeedRatio,
        SeedingTimeHours = e.SeedingTimeHours,
        OldCategory = e.OldCategory,
        NewCategory = e.NewCategory,
        IsCategoryTag = e.IsCategoryTag,
        SearchType = e.SearchType,
        SearchReason = e.SearchReason,
        GrabbedItems = e.GrabbedItems,
    };

    /// <summary>Projects an archived <see cref="EventHistory"/> row into the unified list shape.</summary>
    public static readonly Expression<Func<EventHistory, EventListItem>> FromHistory = h => new EventListItem
    {
        Id = h.Id,
        Timestamp = h.Timestamp,
        EventType = h.EventType,
        Message = h.Message,
        Severity = h.Severity,
        TrackingId = h.TrackingId,
        StrikeId = h.StrikeId,
        JobRunId = h.JobRunId,
        ArrInstanceId = h.ArrInstanceId,
        DownloadClientId = h.DownloadClientId,
        SearchStatus = h.SearchStatus,
        CompletedAt = h.CompletedAt,
        CycleId = h.CycleId,
        IsDryRun = h.IsDryRun,
        ItemTitle = h.ItemTitle,
        ItemHash = h.ItemHash,
        StrikeCount = h.StrikeCount,
        FailedImportReasons = h.FailedImportReasons,
        DeleteReason = h.DeleteReason,
        RemoveFromClient = h.RemoveFromClient,
        CleanReason = h.CleanReason,
        CleanedCategory = h.CleanedCategory,
        SeedRatio = h.SeedRatio,
        SeedingTimeHours = h.SeedingTimeHours,
        OldCategory = h.OldCategory,
        NewCategory = h.NewCategory,
        IsCategoryTag = h.IsCategoryTag,
        SearchType = h.SearchType,
        SearchReason = h.SearchReason,
        GrabbedItems = h.GrabbedItems,
    };
}