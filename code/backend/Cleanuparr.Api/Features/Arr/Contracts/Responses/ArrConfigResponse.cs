using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Arr.Contracts.Responses;

public sealed record ArrConfigResponse
{
    public required Guid Id { get; init; }

    public required InstanceType Type { get; init; }

    public short FailedImportMaxStrikes { get; init; } = -1;

    public List<ArrInstanceResponse> Instances { get; init; } = [];
}
