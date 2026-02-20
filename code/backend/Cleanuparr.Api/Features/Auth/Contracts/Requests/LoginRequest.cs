using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record LoginRequest
{
    [Required]
    public required string Username { get; init; }

    [Required]
    public required string Password { get; init; }
}
