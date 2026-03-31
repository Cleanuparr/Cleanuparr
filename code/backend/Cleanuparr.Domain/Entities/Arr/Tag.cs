namespace Cleanuparr.Domain.Entities.Arr;

public sealed record Tag
{
    public required long Id { get; set; }
    
    public required string Label { get; set; }
}