using System.Linq.Expressions;
using Cleanuparr.Persistence.Models.State;

namespace Cleanuparr.Api.Features.Strikes.Contracts.Responses;

public class RecentStrikeDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string DownloadId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsDryRun { get; set; }

    public static readonly Expression<Func<Strike, RecentStrikeDto>> FromStrike = s => new RecentStrikeDto
    {
        Id = s.Id,
        Type = s.Type.ToString(),
        CreatedAt = s.CreatedAt,
        DownloadId = s.DownloadItem.DownloadId,
        Title = s.DownloadItem.Title,
        IsDryRun = s.IsDryRun,
    };
}
