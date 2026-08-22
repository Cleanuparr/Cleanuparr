using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record VerifyTotpRequest
{
    [Required]
    [StringLength(32)]
    public required string Code { get; init; }
}
