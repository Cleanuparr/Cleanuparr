namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed record PlexAccountInfo
{
    public required string AccountId { get; init; }
    public required string Username { get; init; }
    public string? Email { get; init; }
}
