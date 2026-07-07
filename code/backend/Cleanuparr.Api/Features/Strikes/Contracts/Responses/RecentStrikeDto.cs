namespace Cleanuparr.Api.Features.Strikes.Contracts.Responses;

public class RecentStrikeDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string DownloadId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsDryRun { get; set; }
}
