namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SearchableMovie
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public bool Monitored { get; init; }

    public bool HasFile { get; init; }

    public MovieFileInfo? MovieFile { get; init; }

    public List<string> Tags { get; init; } = [];

    public DateTime? Added { get; init; }
}
