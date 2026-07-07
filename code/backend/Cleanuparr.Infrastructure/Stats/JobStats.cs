namespace Cleanuparr.Infrastructure.Stats;

public class JobStats
{
    public Dictionary<string, JobTypeStats> ByType { get; set; } = new();

    public int TimeframeHours { get; set; }
}
