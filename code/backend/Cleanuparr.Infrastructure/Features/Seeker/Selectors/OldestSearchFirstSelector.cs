namespace Cleanuparr.Infrastructure.Features.Seeker.Selectors;

/// <summary>
/// Selects items that haven't been searched the longest.
/// Items never searched are prioritized first.
/// Provides systematic coverage of the entire library.
/// </summary>
public sealed class OldestSearchFirstSelector : IItemSelector
{
    public List<long> Select(List<(long Id, DateTime? Added, DateTime? LastSearched)> candidates, int count)
    {
        return candidates
            .OrderBy(c => c.LastSearched ?? DateTime.MinValue)
            .Take(count)
            .Select(c => c.Id)
            .ToList();
    }
}
