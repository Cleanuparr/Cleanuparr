using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record SeekerInstanceConfigResponse
{
    public Guid ArrInstanceId { get; init; }
    
    public string InstanceName { get; init; } = string.Empty;
    
    public InstanceType InstanceType { get; init; }
    
    public bool Enabled { get; init; }
    
    public List<string> SkipTags { get; init; } = [];
    
    public DateTime? LastProcessedAt { get; init; }

    public bool ArrInstanceEnabled { get; init; }

    public int ActiveDownloadLimit { get; init; }

    public int MinCycleTimeDays { get; init; }

    public bool MonitoredOnly { get; init; }

    public bool UseCutoff { get; init; }

    public bool UseCustomFormatScore { get; init; }
}