namespace Cleanuparr.Api.Features.Auth.Contracts.Responses;

public sealed record FeatureViewsResponse
{
    /// <summary>
    /// The user's account creation timestamp, used as the anchor for "new feature" detection:
    /// a feature is only considered new if it was first seen meaningfully after this point.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Map of feature id to the UTC timestamp the user first saw it.
    /// </summary>
    public required Dictionary<string, DateTimeOffset> Views { get; init; }
}
