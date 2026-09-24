namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed record PlexPinCheckResult
{
    public required bool Completed { get; init; }
    public string? AuthToken { get; init; }
}
