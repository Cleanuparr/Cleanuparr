namespace Cleanuparr.Domain.Entities.Arr.Queue;

/// <summary>
/// One item in the queue of an *arr application.
/// The *arr applications share this type, and each one sets only its own fields.
/// </summary>
public sealed record QueueRecord
{
    // Sonarr and Whisparr v2
    /// <summary>
    /// The ID of the series.
    /// </summary>
    public long SeriesId { get; init; }

    /// <summary>
    /// The ID of the episode.
    /// </summary>
    public long EpisodeId { get; init; }

    /// <summary>
    /// The number of the season.
    /// </summary>
    public long SeasonNumber { get; init; }

    /// <summary>
    /// The data about the series.
    /// </summary>
    public QueueSeries? Series { get; init; }

    // Radarr and Whisparr v3
    /// <summary>
    /// The ID of the movie.
    /// </summary>
    public long MovieId { get; init; }

    /// <summary>
    /// The data about the movie.
    /// </summary>
    public QueueMovie? Movie { get; init; }

    // Lidarr
    /// <summary>
    /// The ID of the artist.
    /// </summary>
    public long ArtistId { get; init; }

    /// <summary>
    /// The ID of the album.
    /// </summary>
    public long AlbumId { get; init; }

    /// <summary>
    /// The data about the album.
    /// </summary>
    public QueueAlbum? Album { get; init; }

    // Readarr
    /// <summary>
    /// The ID of the author.
    /// </summary>
    public long AuthorId { get; init; }

    /// <summary>
    /// The ID of the book.
    /// </summary>
    public long BookId { get; init; }

    /// <summary>
    /// The data about the book.
    /// </summary>
    public QueueBook? Book { get; init; }

    /// <summary>
    /// The id of the content, for an arr that does not use a number.
    /// LazyLibrarian stores the book id as text.
    /// </summary>
    public string? ContentId { get; init; }

    // common
    /// <summary>
    /// The name of the release.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The status of the queue item, for example "downloading" or "delay".
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// The status of the tracked download, for example "warning".
    /// A pending release does not have a tracked download, and the value is then empty.
    /// </summary>
    public string TrackedDownloadStatus { get; init; } = string.Empty;

    /// <summary>
    /// The state of the tracked download, for example "importBlocked".
    /// A pending release does not have a tracked download, and the value is then empty.
    /// </summary>
    public string TrackedDownloadState { get; init; } = string.Empty;

    /// <summary>
    /// The messages that tell more about the status of the queue item.
    /// </summary>
    public List<TrackedDownloadStatusMessage>? StatusMessages { get; init; }

    /// <summary>
    /// The hash that the download client uses for the item.
    /// A pending release is not in a download client, and the value is then empty.
    /// </summary>
    public string DownloadId { get; init; } = string.Empty;

    /// <summary>
    /// The name of the download client that has the item.
    /// </summary>
    public string? DownloadClient { get; init; }

    /// <summary>
    /// The protocol of the release, for example "torrent" or "usenet".
    /// </summary>
    public string Protocol { get; init; } = string.Empty;

    /// <summary>
    /// The ID of the queue item.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The number of bytes that the download client must still get.
    /// </summary>
    /// <remarks>
    /// Each arr holds this value in a decimal. Sonarr writes its JSON with Newtonsoft.
    /// A whole value then gets a decimal point: 4467066880.0.
    /// </remarks>
    public decimal SizeLeft { get; init; }
}
