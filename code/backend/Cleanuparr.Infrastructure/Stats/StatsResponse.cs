namespace Cleanuparr.Infrastructure.Stats;

public class StatsResponse
{
    public EventStats Events { get; set; } = new();

    public StrikeStats Strikes { get; set; } = new();

    public JobStats Jobs { get; set; } = new();

    public HealthStats Health { get; set; } = new();

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
