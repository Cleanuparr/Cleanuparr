namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;

public sealed record ClientCleanerConfigRequest
{
    public Guid DownloadClientId { get; init; }

    public List<SeedingRuleRequest> SeedingRules { get; init; } = [];

    public UnlinkedConfigRequest? UnlinkedConfig { get; init; }
}
