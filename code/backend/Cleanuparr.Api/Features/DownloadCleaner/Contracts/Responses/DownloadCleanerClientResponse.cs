using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Responses;

public sealed record DownloadCleanerClientResponse
{
    public Guid DownloadClientId { get; init; }

    public required string DownloadClientName { get; init; }

    public bool DownloadClientEnabled { get; init; }

    public DownloadClientTypeName DownloadClientTypeName { get; init; }

    public required IReadOnlyList<SeedingRuleResponse> SeedingRules { get; init; }

    public UnlinkedConfigResponse? UnlinkedConfig { get; init; }

    public OrphanedFilesConfigResponse? OrphanedFilesConfig { get; init; }
}
