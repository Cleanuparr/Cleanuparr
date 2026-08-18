namespace Cleanuparr.Domain.Entities.Arr;

public sealed class BookSearchItem : SearchItem
{
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
