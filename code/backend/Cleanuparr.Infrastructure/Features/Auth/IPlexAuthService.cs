namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed record PlexPinResult
{
    public required int PinId { get; init; }
    public required string PinCode { get; init; }
    public required string AuthUrl { get; init; }
}

public sealed record PlexPinCheckResult
{
    public required bool Completed { get; init; }
    public string? AuthToken { get; init; }
}

public sealed record PlexAccountInfo
{
    public required string AccountId { get; init; }
    public required string Username { get; init; }
    public string? Email { get; init; }
}

public interface IPlexAuthService
{
    Task<PlexPinResult> RequestPin();
    Task<PlexPinCheckResult> CheckPin(int pinId);
    Task<PlexAccountInfo> GetAccount(string authToken);
}
