namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SearchableSeries
{
    public long Id { get; init; }
    
    public string Title { get; init; } = string.Empty;
    
    public bool Monitored { get; init; }
    
    public List<string> Tags { get; init; } = [];
    
    public DateTime? Added { get; init; }
}
