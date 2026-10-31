using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Domain.Entities.Arr;

/// <summary>
/// A Chaptarr book row the Seeker can search for.
/// A title with both formats has one row per side.
/// </summary>
public sealed record SearchableBook
{
    /// <summary>
    /// The row's id in Chaptarr.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The book's title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The author who owns this book.
    /// </summary>
    public long AuthorId { get; init; }

    /// <summary>
    /// Whether Chaptarr monitors this row's side.
    /// </summary>
    public bool Monitored { get; init; }

    /// <summary>
    /// The side this row covers.
    /// </summary>
    public ChaptarrMediaType MediaType { get; init; }

    /// <summary>
    /// The book's release date.
    /// Null when Chaptarr has none.
    /// </summary>
    public DateTimeOffset? ReleaseDate { get; init; }
}
