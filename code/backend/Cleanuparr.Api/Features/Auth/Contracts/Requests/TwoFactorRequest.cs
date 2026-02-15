using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record TwoFactorRequest
{
    [Required]
    public required string LoginToken { get; init; }

    [Required]
    public required string Code { get; init; }

    public bool IsRecoveryCode { get; init; }
}
