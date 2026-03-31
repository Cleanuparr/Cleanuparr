namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SearchableSeries
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public int QualityProfileId { get; init; }

    public bool Monitored { get; init; }

    public List<long> Tags { get; init; } = [];
    
    public DateTime? Added { get; init; }

    public string Status { get; init; } = string.Empty;

    public SeriesStatistics? Statistics { get; init; }
}
