namespace Cleanuparr.Domain.Entities.Arr;

/// <summary>
/// A search request for LazyLibrarian, which identifies a book with text.
/// </summary>
public sealed class BookSearchItem : SearchItem
{
    /// <summary>
    /// The book id, as LazyLibrarian reports it.
    /// </summary>
    public string ContentId { get; set; } = string.Empty;

    public override bool Equals(object? obj)
    {
        if (obj is not BookSearchItem other)
        {
            return false;
        }

        return ContentId == other.ContentId;
    }

    public override int GetHashCode()
    {
        return ContentId.GetHashCode();
    }
}
