using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Stats;

public class EventStats
{
    public int TotalCount { get; set; }

    public Dictionary<string, int> ByType { get; set; } = new();

    public Dictionary<string, int> BySeverity { get; set; } = new();

    public int TimeframeHours { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RecentEventDto>? RecentItems { get; set; }
}
