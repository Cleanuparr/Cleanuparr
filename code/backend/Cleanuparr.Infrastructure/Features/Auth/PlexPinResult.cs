namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed record PlexPinResult
{
    public required int PinId { get; init; }
    public required string PinCode { get; init; }
    public required string AuthUrl { get; init; }
}
