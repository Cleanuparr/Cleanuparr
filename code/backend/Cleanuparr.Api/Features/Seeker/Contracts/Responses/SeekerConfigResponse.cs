using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record SeekerConfigResponse
{
    public bool SearchEnabled { get; init; }

    public ushort SearchInterval { get; init; }

    public bool ProactiveSearchEnabled { get; init; }

    public SelectionStrategy SelectionStrategy { get; init; }

    public bool MonitoredOnly { get; init; }

    public bool UseCutoff { get; init; }

    public bool UseRoundRobin { get; init; }

    public SeriesSearchType SonarrSearchType { get; init; }

    public List<SeekerInstanceConfigResponse> Instances { get; init; } = [];
}
