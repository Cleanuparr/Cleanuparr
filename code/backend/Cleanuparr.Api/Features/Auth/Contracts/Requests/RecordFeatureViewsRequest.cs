using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record RecordFeatureViewsRequest
{
    [Required]
    public required IReadOnlyList<string> FeatureIds { get; init; }
}
