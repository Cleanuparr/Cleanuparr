namespace Cleanuparr.Domain.Entities.Arr;

public class SearchItem
{
    public long Id { get; set; }
    
    public override bool Equals(object? obj)
    {
        if (obj is not SearchItem other)
        {
            return false;
        }
        
        return Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}