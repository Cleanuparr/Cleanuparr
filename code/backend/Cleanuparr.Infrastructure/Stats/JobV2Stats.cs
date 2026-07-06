namespace Cleanuparr.Infrastructure.Stats;

public class JobV2Stats
{
    public int TotalRuns { get; set; }

    public int Completed { get; set; }

    public int Failed { get; set; }

    public Dictionary<string, JobTypeStats> ByType { get; set; } = new();
}
