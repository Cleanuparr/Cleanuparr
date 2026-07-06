namespace Cleanuparr.Infrastructure.Stats;

public class EventV2Stats
{
    public int TotalCount { get; set; }

    public Dictionary<string, int> ByType { get; set; } = new();

    public Dictionary<string, int> BySeverity { get; set; } = new();
}
