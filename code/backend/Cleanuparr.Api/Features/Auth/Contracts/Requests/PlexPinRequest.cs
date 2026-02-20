using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record PlexPinRequest
{
    [Required]
    public required int PinId { get; init; }
}
