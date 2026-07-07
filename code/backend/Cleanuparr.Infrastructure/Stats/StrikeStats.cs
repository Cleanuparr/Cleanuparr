using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Stats;

public class StrikeStats
{
    public int TotalCount { get; set; }

    public Dictionary<string, int> ByType { get; set; } = new();

    public int ItemsRemoved { get; set; }

    public int TimeframeHours { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RecentStrikeDto>? RecentItems { get; set; }
}
