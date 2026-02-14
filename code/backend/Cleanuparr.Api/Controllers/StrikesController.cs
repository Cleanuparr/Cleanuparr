using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.State;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StrikesController : ControllerBase
{
    private readonly EventsContext _context;

    public StrikesController(EventsContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets download items with their strikes (grouped), with pagination and filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<DownloadItemStrikesDto>>> GetStrikes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? type = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 100) pageSize = 100;

        var query = _context.DownloadItems
            .Include(d => d.Strikes)
            .Where(d => d.Strikes.Any());

        // Filter by strike type: only show items that have strikes of this type
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (Enum.TryParse<StrikeType>(type, true, out var strikeType))
                query = query.Where(d => d.Strikes.Any(s => s.Type == strikeType));
        }

        // Apply search filter on title or download hash
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = EventsContext.GetLikePattern(search);
            query = query.Where(d =>
                EF.Functions.Like(d.Title, pattern) ||
                EF.Functions.Like(d.DownloadId, pattern));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = (page - 1) * pageSize;

        var items = await query
            .OrderByDescending(d => d.Strikes.Max(s => s.CreatedAt))
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(d => new DownloadItemStrikesDto
        {
            DownloadItemId = d.Id,
            DownloadId = d.DownloadId,
            Title = d.Title,
            TotalStrikes = d.Strikes.Count,
            StrikesByType = d.Strikes
                .GroupBy(s => s.Type)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()),
            LatestStrikeAt = d.Strikes.Max(s => s.CreatedAt),
            FirstStrikeAt = d.Strikes.Min(s => s.CreatedAt),
            IsMarkedForRemoval = d.IsMarkedForRemoval,
            IsRemoved = d.IsRemoved,
            IsReturning = d.IsReturning,
            Strikes = d.Strikes
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new StrikeDetailDto
                {
                    Id = s.Id,
                    Type = s.Type.ToString(),
                    CreatedAt = s.CreatedAt,
                    LastDownloadedBytes = s.LastDownloadedBytes,
                    JobRunId = s.JobRunId,
                }).ToList(),
        }).ToList();

        return Ok(new PaginatedResult<DownloadItemStrikesDto>
        {
            Items = dtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
        });
    }

    /// <summary>
    /// Gets the most recent individual strikes with download item info (for dashboard)
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<List<RecentStrikeDto>>> GetRecentStrikes(
        [FromQuery] int count = 5)
    {
        if (count < 1) count = 1;
        if (count > 50) count = 50;

        var strikes = await _context.Strikes
            .Include(s => s.DownloadItem)
            .OrderByDescending(s => s.CreatedAt)
            .Take(count)
            .Select(s => new RecentStrikeDto
            {
                Id = s.Id,
                Type = s.Type.ToString(),
                CreatedAt = s.CreatedAt,
                DownloadId = s.DownloadItem.DownloadId,
                Title = s.DownloadItem.Title,
            })
            .ToListAsync();

        return Ok(strikes);
    }

    /// <summary>
    /// Gets all available strike types
    /// </summary>
    [HttpGet("types")]
    public ActionResult<List<string>> GetStrikeTypes()
    {
        var types = Enum.GetNames(typeof(StrikeType)).ToList();
        return Ok(types);
    }

    /// <summary>
    /// Deletes all strikes for a specific download item
    /// </summary>
    [HttpDelete("{downloadItemId:guid}")]
    public async Task<IActionResult> DeleteStrikesForItem(Guid downloadItemId)
    {
        var item = await _context.DownloadItems
            .Include(d => d.Strikes)
            .FirstOrDefaultAsync(d => d.Id == downloadItemId);

        if (item == null)
            return NotFound();

        _context.Strikes.RemoveRange(item.Strikes);
        _context.DownloadItems.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class DownloadItemStrikesDto
{
    public Guid DownloadItemId { get; set; }
    public string DownloadId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int TotalStrikes { get; set; }
    public Dictionary<string, int> StrikesByType { get; set; } = new();
    public DateTime LatestStrikeAt { get; set; }
    public DateTime FirstStrikeAt { get; set; }
    public bool IsMarkedForRemoval { get; set; }
    public bool IsRemoved { get; set; }
    public bool IsReturning { get; set; }
    public List<StrikeDetailDto> Strikes { get; set; } = [];
}

public class StrikeDetailDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long? LastDownloadedBytes { get; set; }
    public Guid JobRunId { get; set; }
}

public class RecentStrikeDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string DownloadId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
