namespace Cleanuparr.Api.Features.Auth.Contracts.Responses;

public sealed record AccountInfoResponse
{
    public required string Username { get; init; }
    public required bool PlexLinked { get; init; }
    public string? PlexUsername { get; init; }
    public required bool TwoFactorEnabled { get; init; }
    public required string ApiKeyPreview { get; init; }
}
