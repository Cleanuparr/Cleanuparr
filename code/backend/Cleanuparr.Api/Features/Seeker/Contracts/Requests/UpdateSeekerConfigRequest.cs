using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Seeker;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Requests;

public sealed record UpdateSeekerConfigRequest
{
    public bool SearchEnabled { get; init; } = true;

    public ushort SearchInterval { get; init; } = 3;

    public bool ProactiveSearchEnabled { get; init; }

    public SelectionStrategy SelectionStrategy { get; init; } = SelectionStrategy.BalancedWeighted;

    public bool MonitoredOnly { get; init; } = true;

    public bool UseCutoff { get; init; }

    public bool UseCustomFormatScore { get; init; }

    public bool UseRoundRobin { get; init; } = true;

    public int PostReleaseGraceHours { get; init; } = 6;

    public List<UpdateSeekerInstanceConfigRequest> Instances { get; init; } = [];

    public SeekerConfig ApplyTo(SeekerConfig config)
    {
        config.SearchEnabled = SearchEnabled;
        config.SearchInterval = SearchInterval;
        config.ProactiveSearchEnabled = ProactiveSearchEnabled;
        config.SelectionStrategy = SelectionStrategy;
        config.MonitoredOnly = MonitoredOnly;
        config.UseCutoff = UseCutoff;
        config.UseCustomFormatScore = UseCustomFormatScore;
        config.UseRoundRobin = UseRoundRobin;
        config.PostReleaseGraceHours = PostReleaseGraceHours;

        return config;
    }
}
