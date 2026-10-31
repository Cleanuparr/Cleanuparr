namespace Cleanuparr.Domain.Entities.Arr;

/// <summary>
/// A Chaptarr author the Seeker can search for.
/// </summary>
public sealed record SearchableAuthor
{
    /// <summary>
    /// The author's id in Chaptarr.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The author's name.
    /// </summary>
    public string AuthorName { get; init; } = string.Empty;

    /// <summary>
    /// Whether Chaptarr monitors this author.
    /// </summary>
    public bool Monitored { get; init; }

    /// <summary>
    /// Tag ids on the author's audiobooks.
    /// Resolve them to labels through Chaptarr's /tag endpoint.
    /// </summary>
    public List<long> AudiobookTags { get; init; } = [];

    /// <summary>
    /// Tag ids on the author's ebooks.
    /// Resolve them to labels through Chaptarr's /tag endpoint.
    /// </summary>
    public List<long> EbookTags { get; init; } = [];

    /// <summary>
    /// When the author was added to Chaptarr.
    /// </summary>
    public DateTimeOffset? Added { get; init; }
}
