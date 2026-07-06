namespace Cleanuparr.Infrastructure.Stats;

public class StatsV2Response
{
    public EventV2Stats Events { get; set; } = new();
    public StrikeV2Stats Strikes { get; set; } = new();
    public MalwareV2Stats Malware { get; set; } = new();
    public JobV2Stats Jobs { get; set; } = new();
    public HealthStats Health { get; set; } = new();

    public DateTimeOffset GeneratedAt { get; set; }
}
