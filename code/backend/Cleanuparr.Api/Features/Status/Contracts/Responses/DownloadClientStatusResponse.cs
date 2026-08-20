using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Status.Contracts.Responses;

public sealed record DownloadClientStatusResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required DownloadClientTypeName Type { get; init; }

    public Uri? Host { get; init; }

    public required bool Enabled { get; init; }

    /// <summary>
    /// Mirrors Enabled. Nothing probes a download client here.
    /// </summary>
    public required bool IsConnected { get; init; }
}
