using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record ChangeUsernameRequest
{
    [Required]
    public required string CurrentPassword { get; init; }

    [Required]
    [MinLength(3)]
    [MaxLength(50)]
    public required string NewUsername { get; init; }
}
