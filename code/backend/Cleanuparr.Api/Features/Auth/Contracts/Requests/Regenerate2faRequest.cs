using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record Regenerate2faRequest
{
    [Required]
    public required string Password { get; init; }

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public required string TotpCode { get; init; }
}
