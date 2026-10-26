using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Requests;

public sealed record UpdateSeekerInstanceConfigRequest
{
    public Guid ArrInstanceId { get; init; }

    public bool Enabled { get; init; } = true;

    public List<string> SkipTags { get; init; } = [];

    public List<string> IncludeTags { get; init; } = [];

    public int ActiveDownloadLimit { get; init; } = 3;

    public bool IgnoreStruckDownloads { get; init; }

    public int MinCycleTimeDays { get; init; } = 7;

    public bool MonitoredOnly { get; init; } = true;

    public bool UseCutoff { get; init; }

    public bool UseCustomFormatScore { get; init; }

    public int RecentGrabWindowHours { get; init; } = 24;

    public SonarrSearchMode SonarrSearchMode { get; init; } = SonarrSearchMode.Auto;

    public RadarrReleaseGate RadarrReleaseGate { get; init; } = RadarrReleaseGate.ReleaseDate;

    public bool RestUnitsEnabled { get; init; }

    public int RestMaxDays { get; init; } = 30;
}