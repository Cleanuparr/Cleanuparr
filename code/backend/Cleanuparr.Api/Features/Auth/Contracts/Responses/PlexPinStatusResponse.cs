namespace Cleanuparr.Api.Features.Auth.Contracts.Responses;

public sealed record PlexPinStatusResponse
{
    public required int PinId { get; init; }
    public required string AuthUrl { get; init; }
}
