namespace Cleanuparr.Api.Features.Strikes.Contracts.Responses;

public class DownloadItemStrikesDto
{
    public Guid DownloadItemId { get; set; }
    public string DownloadId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int TotalStrikes { get; set; }
    public Dictionary<string, int> StrikesByType { get; set; } = new();
    public DateTimeOffset LatestStrikeAt { get; set; }
    public DateTimeOffset FirstStrikeAt { get; set; }
    public bool IsMarkedForRemoval { get; set; }
    public bool IsRemoved { get; set; }
    public bool IsReturning { get; set; }
    public bool HasDryRunStrikes { get; set; }
    public List<StrikeDetailDto> Strikes { get; set; } = [];
}
