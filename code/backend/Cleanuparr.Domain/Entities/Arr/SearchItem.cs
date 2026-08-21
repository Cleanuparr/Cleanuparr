using System.Text.Json.Serialization;

namespace Cleanuparr.Domain.Entities.Arr;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$item")]
[JsonDerivedType(typeof(SearchItem), "base")]
[JsonDerivedType(typeof(SeriesSearchItem), "series")]
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