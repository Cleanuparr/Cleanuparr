using System.Text.Json.Serialization;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    public async Task<ActionResult<PaginatedResult<AppEvent>>> GetEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? severity = null,
        [FromQuery] string? eventType = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? search = null)
    {
        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 100;
        if (pageSize > 1000) pageSize = 1000; // Cap at 1000 for performance
        
        var query = _context.Events.AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(severity))
        {
            if (Enum.TryParse<EventSeverity>(severity, true, out var severityEnum))
                query = query.Where(e => e.Severity == severityEnum);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            if (Enum.TryParse<EventType>(eventType, true, out var eventTypeEnum))
                query = query.Where(e => e.EventType == eventTypeEnum);
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

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = EventsContext.GetLikePattern(search);
            query = query.Where(e =>
                EF.Functions.Like(e.Message, pattern) ||
                EF.Functions.Like(e.Data, pattern) ||
                EF.Functions.Like(e.TrackingId.ToString(), pattern)
            );
        }

        // Count total matching records for pagination
        var totalCount = await query.CountAsync();
        
        // Calculate pagination
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = (page - 1) * pageSize;
        
        // Get paginated data
        var events = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        // Return paginated result
        var result = new PaginatedResult<AppEvent>
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
    /// Gets event statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetEventStats()
    {
        var stats = new
        {
            TotalEvents = await _context.Events.CountAsync(),
            EventsBySeverity = await _context.Events
                .GroupBy(e => e.Severity)
                .Select(g => new { Severity = g.Key.ToString(), Count = g.Count() })
                .ToListAsync(),
            EventsByType = await _context.Events
                .GroupBy(e => e.EventType)
                .Select(g => new { EventType = g.Key.ToString(), Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync(),
            RecentEventsCount = await _context.Events
                .Where(e => e.Timestamp > DateTime.UtcNow.AddHours(-24))
                .CountAsync()
        };

        return Ok(stats);
    }

    /// <summary>
    /// Manually triggers cleanup of old events
    /// </summary>
    [HttpPost("cleanup")]
    public async Task<ActionResult<object>> CleanupOldEvents([FromQuery] int retentionDays = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        
        await _context.Events
            .Where(e => e.Timestamp < cutoffDate)
            .ExecuteDeleteAsync();
        
        return Ok();
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