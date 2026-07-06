namespace Cleanuparr.Infrastructure.Stats;

public class RecentEventDto
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}
