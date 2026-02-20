using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record VerifyTotpRequest
{
    [Required]
    [StringLength(6, MinimumLength = 6)]
    public required string Code { get; init; }
}
