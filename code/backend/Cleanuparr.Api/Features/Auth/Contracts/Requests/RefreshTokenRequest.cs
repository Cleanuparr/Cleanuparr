using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record RefreshTokenRequest
{
    [Required]
    public required string RefreshToken { get; init; }
}
