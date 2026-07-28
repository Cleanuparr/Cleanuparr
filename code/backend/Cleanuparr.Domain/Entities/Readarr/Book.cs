namespace Cleanuparr.Domain.Entities.Readarr;

/// <summary>
/// A book in Readarr.
/// </summary>
public sealed record Book
{
    /// <summary>
    /// The ID of the book.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The name of the book.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The ID of the author of the book.
    /// </summary>
    public long AuthorId { get; set; }

    /// <summary>
    /// The data about the author of the book.
    /// </summary>
    public Author Author { get; set; } = new();
}
