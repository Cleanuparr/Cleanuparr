namespace Cleanuparr.Infrastructure.Features.Auth.Models;

internal sealed class OidcFlowState
{
    public required string State { get; init; }
    public required string Nonce { get; init; }
    public required string CodeVerifier { get; init; }
    public required string RedirectUri { get; init; }
    public string? InitiatorUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
