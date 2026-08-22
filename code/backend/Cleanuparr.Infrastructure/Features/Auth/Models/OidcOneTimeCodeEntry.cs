namespace Cleanuparr.Infrastructure.Features.Auth.Models;

internal sealed class OidcOneTimeCodeEntry
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required int ExpiresIn { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
