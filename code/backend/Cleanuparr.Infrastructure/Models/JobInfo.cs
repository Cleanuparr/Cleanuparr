namespace Cleanuparr.Infrastructure.Models;

public class JobInfo
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public DateTimeOffset? NextRunTime { get; set; }
    public DateTimeOffset? PreviousRunTime { get; set; }
    public string JobType { get; set; } = string.Empty;
}
