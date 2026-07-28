namespace Cleanuparr.Domain.Entities.Radarr;

public sealed record Movie
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;
}