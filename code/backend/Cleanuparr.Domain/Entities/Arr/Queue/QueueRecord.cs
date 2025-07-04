namespace Cleanuparr.Domain.Entities.Arr.Queue;

public sealed record QueueRecord
{
    // Sonarr and Whisparr
    public long SeriesId { get; init; }
    public long EpisodeId { get; init; }
    public long SeasonNumber { get; init; }
    
    public QueueSeries? Series { get; init; }
    
    // Radarr
    public long MovieId { get; init; }
    
    public QueueSeries? Movie { get; init; }
    
    // Lidarr
    public long ArtistId { get; init; }
    
    public long AlbumId { get; init; }
    
    public QueueAlbum? Album { get; init; }
    
    // Readarr
    public long AuthorId { get; init; }
    
    public long BookId { get; init; }
    
    public QueueBook? Book { get; init; }
    
    // common
    public required string Title { get; init; }
    public string Status { get; init; }
    public string TrackedDownloadStatus { get; init; }
    public string TrackedDownloadState { get; init; }
    public List<TrackedDownloadStatusMessage>? StatusMessages { get; init; }
    public required string DownloadId { get; init; }
    public required string Protocol { get; init; }
    public required long Id { get; init; }
}