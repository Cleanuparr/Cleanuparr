namespace Cleanuparr.Infrastructure.Stats;

public class JobTypeStats
{
    public int TotalRuns { get; set; }

    public int Completed { get; set; }

    public int Failed { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    public DateTimeOffset? NextRunAt { get; set; }
}
