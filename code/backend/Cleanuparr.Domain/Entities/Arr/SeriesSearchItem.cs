using Cleanuparr.Domain.Enums;
using Data.Models.Arr;

namespace Cleanuparr.Domain.Entities.Arr;

public sealed class SeriesSearchItem : SearchItem
{
    public long SeriesId { get; set; }
    
    public SeriesSearchType SearchType { get; set; }
    
    public override bool Equals(object? obj)
    {
        if (obj is not SeriesSearchItem other)
        {
            return false;
        }
        
        return Id == other.Id && SeriesId == other.SeriesId;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, SeriesId);
    }
}