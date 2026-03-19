namespace Cleanuparr.Domain.Entities.Arr;

/// <summary>
/// Represents the custom format score data from a movie/episode file API response
/// </summary>
public sealed record MediaFileScore
{
    public long Id { get; init; }

    public int CustomFormatScore { get; init; }
}
