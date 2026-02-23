using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record Enable2faRequest
{
    [Required]
    public required string Password { get; init; }
}
