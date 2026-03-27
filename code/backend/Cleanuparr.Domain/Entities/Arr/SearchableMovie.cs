namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SearchableMovie
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public bool Monitored { get; init; }

    public bool HasFile { get; init; }

    public MovieFileInfo? MovieFile { get; init; }

    public List<string> Tags { get; init; } = [];

    public int QualityProfileId { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTime? Added { get; init; }

    public DateTime? DigitalRelease { get; init; }

    public DateTime? PhysicalRelease { get; init; }

    public DateTime? InCinemas { get; init; }
}
