using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Seeker;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Requests;

public sealed record UpdateSeekerConfigRequest
{
    public bool SearchEnabled { get; init; } = true;

    public ushort SearchInterval { get; init; } = 3;

    public bool ProactiveSearchEnabled { get; init; }

    public SelectionStrategy SelectionStrategy { get; init; } = SelectionStrategy.BalancedWeighted;

    public bool UseRoundRobin { get; init; } = true;

    public int PostReleaseGraceHours { get; init; } = 6;

    public bool QuietHoursEnabled { get; init; }

    public TimeOnly QuietHoursStart { get; init; } = new(23, 0);

    public TimeOnly QuietHoursEnd { get; init; } = new(7, 0);

    public List<UpdateSeekerInstanceConfigRequest> Instances { get; init; } = [];

    public SeekerConfig ApplyTo(SeekerConfig config)
    {
        config.SearchEnabled = SearchEnabled;
        config.SearchInterval = SearchInterval;
        config.ProactiveSearchEnabled = ProactiveSearchEnabled;
        config.SelectionStrategy = SelectionStrategy;
        config.UseRoundRobin = UseRoundRobin;
        config.PostReleaseGraceHours = PostReleaseGraceHours;
        config.QuietHoursEnabled = QuietHoursEnabled;
        config.QuietHoursStart = QuietHoursStart;
        config.QuietHoursEnd = QuietHoursEnd;

        return config;
    }
}
