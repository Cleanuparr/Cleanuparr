namespace Cleanuparr.Domain.Entities.Sonarr;

public sealed record Series
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;
}