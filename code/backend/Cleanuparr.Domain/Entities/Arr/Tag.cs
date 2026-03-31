namespace Cleanuparr.Domain.Entities.Arr;

public sealed record Tag
{
    public required long Id { get; init; }
    
    public required string Label { get; init; }
}