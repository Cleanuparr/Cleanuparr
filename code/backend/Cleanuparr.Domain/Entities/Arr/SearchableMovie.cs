namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SearchableMovie
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public bool Monitored { get; init; }

    public bool HasFile { get; init; }

    public MovieFileInfo? MovieFile { get; init; }

    public List<long> Tags { get; init; } = [];

    public int QualityProfileId { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset? Added { get; init; }

    public DateTimeOffset? DigitalRelease { get; init; }

    public DateTimeOffset? PhysicalRelease { get; init; }

    public DateTimeOffset? InCinemas { get; init; }
}
