namespace Cleanuparr.Api.Features.Strikes.Contracts.Responses;

public class StrikeDetailDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public long? LastDownloadedBytes { get; set; }
    public Guid JobRunId { get; set; }
    public bool IsDryRun { get; set; }
}
