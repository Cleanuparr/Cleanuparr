using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.DownloadClient.Contracts.Responses;

/// <summary>
/// What a download client type's connection form needs and what its service can do, so the frontend
/// can build the form and feature set from the backend instead of hardcoding per-type lists.
/// </summary>
public sealed record DownloadClientTypeResponse
{
    public required DownloadClientTypeName TypeName { get; init; }

    public required List<DownloadClientAuthField> AuthFields { get; init; }

    public required List<DownloadClientCapability> Capabilities { get; init; }

    public static DownloadClientTypeResponse From(DownloadClientTypeName typeName) => new()
    {
        TypeName = typeName,
        AuthFields = Enum.GetValues<DownloadClientAuthField>()
            .Where(field => typeName.SupportsAuthField(field))
            .ToList(),
        Capabilities = Enum.GetValues<DownloadClientCapability>()
            .Where(capability => typeName.SupportsCapability(capability))
            .ToList(),
    };
}
