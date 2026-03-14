using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Seeker;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Requests;

public sealed record UpdateSeekerConfigRequest
{
    public bool Enabled { get; init; }

    public string CronExpression { get; init; } = "0 0 * * * ?";

    public bool UseAdvancedScheduling { get; init; }

    public SelectionStrategy SelectionStrategy { get; init; } = SelectionStrategy.BalancedWeighted;

    public bool MonitoredOnly { get; init; } = true;

    public bool UseCutoff { get; init; }

    public bool UseRoundRobin { get; init; } = true;

    public List<UpdateSeekerInstanceConfigRequest> Instances { get; init; } = [];

    public SeekerConfig ApplyTo(SeekerConfig config)
    {
        config.Enabled = Enabled;
        config.CronExpression = CronExpression;
        config.UseAdvancedScheduling = UseAdvancedScheduling;
        config.SelectionStrategy = SelectionStrategy;
        config.MonitoredOnly = MonitoredOnly;
        config.UseCutoff = UseCutoff;
        config.UseRoundRobin = UseRoundRobin;

        return config;
    }
}
