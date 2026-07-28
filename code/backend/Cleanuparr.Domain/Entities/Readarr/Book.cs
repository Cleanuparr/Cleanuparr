namespace Cleanuparr.Domain.Entities.Readarr;

public sealed record Book
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;
    
    public long AuthorId { get; set; }
    
    public Author Author { get; set; } = new();
} 