using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record SeekerConfigResponse
{
    public bool Enabled { get; init; }
    
    public string CronExpression { get; init; } = string.Empty;

    public bool UseAdvancedScheduling { get; init; }

    public SelectionStrategy SelectionStrategy { get; init; }
    
    public bool MonitoredOnly { get; init; }
    
    public bool UseCutoff { get; init; }

    public bool UseRoundRobin { get; init; }

    public List<SeekerInstanceConfigResponse> Instances { get; init; } = [];
}
