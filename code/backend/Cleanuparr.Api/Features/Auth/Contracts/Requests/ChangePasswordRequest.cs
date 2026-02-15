using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record ChangePasswordRequest
{
    [Required]
    public required string CurrentPassword { get; init; }

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public required string NewPassword { get; init; }
}
