namespace Cleanuparr.Domain.Entities.Whisparr;

public sealed record WhisparrV3Command
{
    public required string Name { get; init; }
    
    public required List<long> MovieIds { get; init; }
}