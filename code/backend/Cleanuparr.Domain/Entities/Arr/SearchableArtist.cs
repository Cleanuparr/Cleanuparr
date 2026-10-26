namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SearchableArtist
{
    public long Id { get; init; }

    public string ArtistName { get; init; } = string.Empty;

    public bool Monitored { get; init; }

    public List<long> Tags { get; init; } = [];

    public int QualityProfileId { get; init; }

    public DateTimeOffset? Added { get; init; }

    public string Status { get; init; } = string.Empty;
}
