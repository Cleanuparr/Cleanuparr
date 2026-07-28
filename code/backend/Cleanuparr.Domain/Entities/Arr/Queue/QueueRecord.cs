namespace Cleanuparr.Domain.Entities.Arr.Queue;

public sealed record QueueRecord
{
    // Sonarr and Whisparr v2
    public long SeriesId { get; init; }
    public long EpisodeId { get; init; }
    public long SeasonNumber { get; init; }
    
    public QueueSeries? Series { get; init; }
    
    // Radarr and Whisparr v3
    public long MovieId { get; init; }
    
    public QueueMovie? Movie { get; init; }
    
    // Lidarr
    public long ArtistId { get; init; }
    
    public long AlbumId { get; init; }
    
    public QueueAlbum? Album { get; init; }
    
    // Readarr
    public long AuthorId { get; init; }
    
    public long BookId { get; init; }
    
    public QueueBook? Book { get; init; }
    
    // common
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string TrackedDownloadStatus { get; init; } = string.Empty;
    public string TrackedDownloadState { get; init; } = string.Empty;
    public List<TrackedDownloadStatusMessage>? StatusMessages { get; init; }
    public string DownloadId { get; init; } = string.Empty;
    public string? DownloadClient { get; init; }
    public string Protocol { get; init; } = string.Empty;
    public long Id { get; init; }
    public long SizeLeft { get; init; }
}