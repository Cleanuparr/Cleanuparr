namespace Cleanuparr.Domain.Entities.Arr;

public sealed record Tag
{
    public long Id { get; init; }

    public string Label { get; init; } = string.Empty;
}