namespace Cleanuparr.Infrastructure.Features.Seeker.Selectors;

/// <summary>
/// Selects items using rank-based weighted random sampling on add date.
/// Recently added items are ranked higher and more likely to be selected.
/// Good for users who regularly add new content and want it searched quickly.
/// </summary>
public sealed class NewestWeightedSelector : IItemSelector
{
    public List<long> Select(List<(long Id, DateTime? Added, DateTime? LastSearched)> candidates, int count)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        count = Math.Min(count, candidates.Count);

        // Sort by Added descending
        var ranked = candidates
            .OrderByDescending(c => c.Added ?? DateTime.MinValue)
            .ToList();

        return OldestSearchWeightedSelector.WeightedRandomByRank(ranked, count);
    }
}
