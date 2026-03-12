using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record OidcExchangeRequest
{
    [Required]
    public required string Code { get; init; }
}
