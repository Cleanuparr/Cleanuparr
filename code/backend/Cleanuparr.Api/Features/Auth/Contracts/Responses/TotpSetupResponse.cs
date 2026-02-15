namespace Cleanuparr.Api.Features.Auth.Contracts.Responses;

public sealed record TotpSetupResponse
{
    public required string Secret { get; init; }
    public required string QrCodeUri { get; init; }
    public required List<string> RecoveryCodes { get; init; }
}
