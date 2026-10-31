using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record SeekerInstanceConfigResponse
{
    public Guid ArrInstanceId { get; init; }
    
    public string InstanceName { get; init; } = string.Empty;
    
    public InstanceType InstanceType { get; init; }

    public float InstanceVersion { get; init; }
    
    public bool Enabled { get; init; }
    
    public List<string> SkipTags { get; init; } = [];
    
    public List<string> IncludeTags { get; init; } = [];

    public DateTimeOffset? LastProcessedAt { get; init; }

    public bool ArrInstanceEnabled { get; init; }

    public int ActiveDownloadLimit { get; init; }

    public bool IgnoreStruckDownloads { get; init; }

    public int MinCycleTimeDays { get; init; }

    public bool MonitoredOnly { get; init; }

    public bool UseCutoff { get; init; }

    public bool UseCustomFormatScore { get; init; }

    /// <summary>
    /// True when the custom format score toggle applies to this instance type.
    /// </summary>
    public bool ReadsCustomFormatScores { get; init; }

    /// <summary>
    /// False when the instance type follows each item's own monitored flag.
    /// </summary>
    public bool ChoosesMonitoredOnly { get; init; }

    public int RecentGrabWindowHours { get; init; }

    public SonarrSearchMode SonarrSearchMode { get; init; }

    public RadarrReleaseGate RadarrReleaseGate { get; init; }

    public bool RestUnitsEnabled { get; init; }

    public int RestMaxDays { get; init; }
}