using System.Globalization;
using Cleanuparr.Api.Common;
using Cleanuparr.Api.Contracts.Responses;
using Cleanuparr.Api.Features.Events.Contracts.Responses;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Stats;
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
            .Select(EventListItem.FromEvent);

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
        hours = TimelineWindow.ClampHours(hours);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset cutoff = now.AddHours(-hours);
        bool hourly = TimelineBucketing.IsHourly(hours);
        string cutoffText = cutoff.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture);

        string bucketExpr = $"substr(timestamp, 1, {TimelineBucketing.KeyLength(hourly)})";
        List<BucketTypeCount> rows = await _context.Database
            .SqlQueryRaw<BucketTypeCount>(
                $$"""
                SELECT {{bucketExpr}} AS "bucket", event_type AS "event_type", COUNT(*) AS "count"
                FROM events
                WHERE timestamp >= {0}
                GROUP BY {{bucketExpr}}, event_type
                """,
                cutoffText)
            .ToListAsync();

        Dictionary<(DateTimeOffset Bucket, EventType Type), int> byBucketType = new();
        HashSet<EventType> presentSet = [];
        foreach (BucketTypeCount row in rows)
        {
            DateTimeOffset bucket = TimelineBucketing.ParseKey(row.Bucket, hourly);
            EventType type = Enum.Parse<EventType>(row.EventType, ignoreCase: true);
            byBucketType[(bucket, type)] = row.Count;
            presentSet.Add(type);
        }

        List<EventType> presentTypes = presentSet
            .OrderBy(t => (int)t)
            .ToList();

        List<EventTypeTimelineBucket> buckets = [];
        foreach (DateTimeOffset bucket in TimelineBucketing.Buckets(cutoff, now, hourly))
        {
            Dictionary<string, int> counts = new();
            foreach (EventType type in presentTypes)
            {
                if (byBucketType.TryGetValue((bucket, type), out int count) && count > 0)
                {
                    counts[type.ToString()] = count;
                }
            }

            buckets.Add(new EventTypeTimelineBucket { Date = bucket, Counts = counts });
        }

        return Ok(new EventTypeTimelineResponse
        {
            Types = presentTypes.Select(t => t.ToString()).ToList(),
            Buckets = buckets,
        });
    }

    private sealed class BucketTypeCount
    {
        public string Bucket { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
