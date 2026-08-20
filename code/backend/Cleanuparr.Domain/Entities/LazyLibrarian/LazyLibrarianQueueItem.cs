using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Domain.Entities.LazyLibrarian;

/// <summary>
/// A snatched LazyLibrarian row that Cleanuparr can act on.
/// </summary>
public sealed record LazyLibrarianQueueItem
{
    public required string DownloadId { get; init; }

    public required string Title { get; init; }

    /// <summary>
    /// The book id the queueBook and searchBook commands accept.
    /// </summary>
    public required string BookId { get; init; }

    public required BookLibrary Library { get; init; }

    public required LazyLibrarianSource Source { get; init; }

    /// <summary>
    /// Adopted when any row sharing this DownloadId reports an origin other than new.
    /// LazyLibrarian refuses to remove such a task.
    /// </summary>
    public required LazyLibrarianOrigin Origin { get; init; }

    public bool IsAudioBook => Library is BookLibrary.AudioBook;

    public bool WasAdoptedByLazyLibrarian => Origin is not LazyLibrarianOrigin.New;
}
