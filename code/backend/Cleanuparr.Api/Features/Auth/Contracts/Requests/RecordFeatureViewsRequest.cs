using System.ComponentModel.DataAnnotations;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

/// <summary>
/// Request to record that the current user has seen the given features, used to drive the "NEW" feature badges in the UI.
/// </summary>
public sealed record RecordFeatureViewsRequest
{
    /// <summary>
    /// The feature identifiers the user has been exposed to.
    /// Unknown ids are recorded with the current timestamp; already-seen ids are ignored.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required IReadOnlyList<string> FeatureIds { get; init; }
}
