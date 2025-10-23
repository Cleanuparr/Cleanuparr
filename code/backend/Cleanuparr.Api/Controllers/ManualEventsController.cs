using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ManualEventsController : ControllerBase
{
    private readonly EventsContext _context;

    public ManualEventsController(EventsContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets manual events with pagination and filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<ManualEvent>>> GetManualEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] bool? isResolved = null,
        [FromQuery] string? severity = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? search = null)
    {
        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 100;
        if (pageSize > 1000) pageSize = 1000; // Cap at 1000 for performance

        var query = _context.ManualEvents.AsQueryable();

        // Apply filters
        if (isResolved.HasValue)
        {
            query = query.Where(e => e.IsResolved == isResolved.Value);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            if (Enum.TryParse<EventSeverity>(severity, true, out var severityEnum))
                query = query.Where(e => e.Severity == severityEnum);
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
                EF.Functions.Like(e.Data, pattern)
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
        var result = new PaginatedResult<ManualEvent>
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
    /// Gets a specific manual event by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ManualEvent>> GetManualEvent(Guid id)
    {
        var eventEntity = await _context.ManualEvents.FindAsync(id);

        if (eventEntity == null)
            return NotFound();

        return Ok(eventEntity);
    }

    /// <summary>
    /// Marks a manual event as resolved
    /// </summary>
    [HttpPost("{id}/resolve")]
    public async Task<ActionResult> ResolveManualEvent(Guid id)
    {
        var eventEntity = await _context.ManualEvents.FindAsync(id);

        if (eventEntity == null)
            return NotFound();

        eventEntity.IsResolved = true;
        await _context.SaveChangesAsync();

        return Ok();
    }

    /// <summary>
    /// Gets manual event statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetManualEventStats()
    {
        var stats = new
        {
            TotalEvents = await _context.ManualEvents.CountAsync(),
            UnresolvedEvents = await _context.ManualEvents.CountAsync(e => !e.IsResolved),
            ResolvedEvents = await _context.ManualEvents.CountAsync(e => e.IsResolved),
            EventsBySeverity = await _context.ManualEvents
                .GroupBy(e => e.Severity)
                .Select(g => new { Severity = g.Key.ToString(), Count = g.Count() })
                .ToListAsync(),
            UnresolvedBySeverity = await _context.ManualEvents
                .Where(e => !e.IsResolved)
                .GroupBy(e => e.Severity)
                .Select(g => new { Severity = g.Key.ToString(), Count = g.Count() })
                .ToListAsync()
        };

        return Ok(stats);
    }

    /// <summary>
    /// Gets unique severities for manual events
    /// </summary>
    [HttpGet("severities")]
    public async Task<ActionResult<List<string>>> GetSeverities()
    {
        var severities = Enum.GetNames(typeof(EventSeverity)).ToList();
        return Ok(severities);
    }

    /// <summary>
    /// Manually triggers cleanup of old resolved events
    /// </summary>
    [HttpPost("cleanup")]
    public async Task<ActionResult<object>> CleanupOldResolvedEvents([FromQuery] int retentionDays = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        var deletedCount = await _context.ManualEvents
            .Where(e => e.IsResolved && e.Timestamp < cutoffDate)
            .ExecuteDeleteAsync();

        return Ok(new { DeletedCount = deletedCount });
    }
}
