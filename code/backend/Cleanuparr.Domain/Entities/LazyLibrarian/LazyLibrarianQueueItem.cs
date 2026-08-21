using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Domain.Entities.LazyLibrarian;

/// <summary>
/// A snatched LazyLibrarian download that Cleanuparr can act on.
/// </summary>
public sealed record LazyLibrarianQueueItem
{
    public required string DownloadId { get; init; }

    public required string Title { get; init; }

    /// <summary>
    /// Every book this download was snatched for.
    /// </summary>
    public required IReadOnlyList<LazyLibrarianBookRef> Books { get; init; }

    public required LazyLibrarianSource Source { get; init; }

    /// <summary>
    /// Adopted when any row sharing this DownloadId reports an origin other than new.
    /// LazyLibrarian refuses to remove such a task.
    /// </summary>
    public required LazyLibrarianOrigin Origin { get; init; }

    public bool WasAdoptedByLazyLibrarian => Origin is not LazyLibrarianOrigin.New;
}
