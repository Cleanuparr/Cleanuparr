namespace Cleanuparr.Infrastructure.Stats;

public class StrikeV2Stats
{
    public Dictionary<string, int> Active { get; set; } = new();

    public int Issued { get; set; }

    public int Recovered { get; set; }

    public int Removed { get; set; }
}
