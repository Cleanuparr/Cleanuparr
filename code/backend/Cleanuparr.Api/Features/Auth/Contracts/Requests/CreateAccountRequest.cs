using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record CreateAccountRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(50)]
    public required string Username { get; init; }

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public required string Password { get; init; }
}
