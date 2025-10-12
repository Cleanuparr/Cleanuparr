namespace Cleanuparr.Api.Features.Arr.Contracts.Requests;

public sealed record UpdateArrConfigRequest
{
    public short FailedImportMaxStrikes { get; init; } = -1;
}
