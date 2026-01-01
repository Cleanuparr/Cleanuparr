using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Domain.Entities.Whisparr;

public sealed record WhisparrV2Command
{
    public string Name { get; set; }

    public long? SeriesId { get; set; }
    
    public long? SeasonNumber { get; set; }
    
    public List<long>? EpisodeIds { get; set; }
    
    public SeriesSearchType SearchType { get; set; }
} 